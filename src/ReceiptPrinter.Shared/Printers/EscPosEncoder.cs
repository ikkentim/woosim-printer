using System.Text;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Printers;

/// <summary>
/// Turns a <see cref="Receipt"/> into the raw ESC/POS byte stream a Woosim printer consumes over
/// serial. This is the entire "how to talk to the printer" translation, kept transport-agnostic:
/// the direct-serial path (<c>SerialWoosimPrinter</c>) writes the bytes straight to the COM port, and
/// the network path (<c>NetworkWoosimPrinter</c>) ships the exact same bytes over HTTP - so whatever
/// sits next to the printer (today <c>ReceiptPrinter.NetworkSerialService</c>, tomorrow the ESP32
/// firmware in docs/HARDWARE.md) only has to copy them to the UART, with no notion of receipts.
/// </summary>
public static class EscPosEncoder
{
    private const byte ESC = 0x1B;
    private const byte GS = 0x1D;
    private const byte LF = 0x0A;

    // 24-dot double density: one ESC * band is 24 dots tall, square-ish pixels at the head's 203 DPI.
    private const int BandHeight = 24;

    private static readonly Encoding Cp437;

    static EscPosEncoder()
    {
        // Woosim printers speak plain code page 437 for latin text - .NET Core needs this registered
        // explicitly to resolve it.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp437 = Encoding.GetEncoding(437);
    }

    /// <summary>
    /// Encodes <paramref name="receipt"/> as a self-contained ESC/POS stream: a leading <c>ESC @</c>
    /// reset, then each element, then the paper cut. Self-contained on purpose - a dumb passthrough
    /// (the network service / ESP32 firmware) can forward it byte-for-byte with no per-connection
    /// setup of its own.
    /// </summary>
    public static byte[] Encode(Receipt receipt)
    {
        var buffer = new List<byte>(256);

        // Reset to a known state (clears bold/underline/size/justification left by a previous job).
        Raw(buffer, ESC, (byte)'@');

        foreach (var element in receipt.Elements)
        {
            switch (element)
            {
                case TextElement text:
                    RenderText(buffer, text);
                    break;
                case ImageElement image:
                    RenderImage(buffer, image);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported element type: {element.GetType().Name}");
            }
        }

        Raw(buffer, GS, (byte)'V', (byte)receipt.Cut);

        return buffer.ToArray();
    }

    private static void RenderText(List<byte> buffer, TextElement text)
    {
        var width = Math.Clamp(text.Width, 1, 8);
        var height = Math.Clamp(text.Height, 1, 8);

        Raw(buffer, ESC, (byte)'a', (byte)text.Justification);
        Raw(buffer, GS, (byte)'!', (byte)(((width - 1) << 4) | (height - 1)));
        Raw(buffer, ESC, (byte)'E', (byte)(text.Bold ? 1 : 0));
        Raw(buffer, ESC, (byte)'-', (byte)(text.Underline ? 1 : 0));
        buffer.AddRange(Cp437.GetBytes(text.LineBreak ? text.Text + "\n" : text.Text));
    }

    /// <summary>
    /// Emits the image as a stack of <c>ESC *</c> bit-image bands. Line spacing is pinned to the band
    /// height for the duration so the bands butt together with no white gaps, then restored.
    /// </summary>
    private static void RenderImage(List<byte> buffer, ImageElement image)
    {
        var stride = (image.Width + 7) / 8;

        Raw(buffer, ESC, (byte)'a', (byte)image.Justification);
        Raw(buffer, ESC, (byte)'3', BandHeight); // set line spacing to n dots

        for (var top = 0; top < image.Height; top += BandHeight)
        {
            Raw(buffer, ESC, (byte)'*', 33, (byte)(image.Width & 0xFF), (byte)(image.Width >> 8));

            for (var x = 0; x < image.Width; x++)
            {
                for (var slice = 0; slice < 3; slice++)
                {
                    byte column = 0;
                    for (var bit = 0; bit < 8; bit++)
                    {
                        var y = top + slice * 8 + bit;
                        if (y < image.Height && (image.Rows[y * stride + (x >> 3)] & (0x80 >> (x & 7))) != 0)
                            column |= (byte)(0x80 >> bit);
                    }
                    buffer.Add(column);
                }
            }

            buffer.Add(LF);
        }

        Raw(buffer, ESC, (byte)'2'); // restore default line spacing
    }

    private static void Raw(List<byte> buffer, params byte[] data) => buffer.AddRange(data);
}
