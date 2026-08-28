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

    static EscPosEncoder()
    {
        // Woosim printers speak plain code page 437 for latin text - .NET Core needs this registered
        // explicitly to resolve it.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Encodes <paramref name="receipt"/> as a self-contained ESC/POS stream: a leading <c>ESC @</c>
    /// reset, then each element, then the paper cut. Self-contained on purpose - a dumb passthrough
    /// (the network service / ESP32 firmware) can forward it byte-for-byte with no per-connection
    /// setup of its own.
    /// </summary>
    public static byte[] Encode(Receipt receipt)
    {
        var cp437 = Encoding.GetEncoding(437);
        var buffer = new List<byte>(256);

        void Raw(params byte[] data) => buffer.AddRange(data);

        // Reset to a known state (clears bold/underline/size/justification left by a previous job).
        Raw(ESC, (byte)'@');

        foreach (var element in receipt.Elements)
        {
            switch (element)
            {
                case TextElement text:
                    var width = Math.Clamp(text.Width, 1, 8);
                    var height = Math.Clamp(text.Height, 1, 8);

                    Raw(ESC, (byte)'a', (byte)text.Justification);
                    Raw(GS, (byte)'!', (byte)(((width - 1) << 4) | (height - 1)));
                    Raw(ESC, (byte)'E', (byte)(text.Bold ? 1 : 0));
                    Raw(ESC, (byte)'-', (byte)(text.Underline ? 1 : 0));
                    buffer.AddRange(cp437.GetBytes(text.LineBreak ? text.Text + "\n" : text.Text));
                    break;

                default:
                    throw new NotSupportedException($"Unsupported element type: {element.GetType().Name}");
            }
        }

        Raw(GS, (byte)'V', (byte)receipt.Cut);

        return buffer.ToArray();
    }
}
