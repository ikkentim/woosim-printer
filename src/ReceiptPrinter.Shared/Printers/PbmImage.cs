using System.Text;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Printers;

/// <summary>
/// Minimal reader for binary PBM ("P4") 1-bit bitmaps - the format the bundled weather icons ship in
/// (that's what ImageMagick emits for a <c>.pbm</c>). A set bit is a black dot, matching what the
/// ESC/POS bit-image command expects, so the bytes carry straight through to <see cref="ImageElement"/>.
/// </summary>
public static class PbmImage
{
    public static ImageElement Parse(byte[] data, Justification justification = Justification.Left)
    {
        var pos = 0;
        if (ReadToken(data, ref pos) != "P4")
            throw new FormatException("Not a binary PBM (P4) image.");

        var width = int.Parse(ReadToken(data, ref pos));
        var height = int.Parse(ReadToken(data, ref pos));
        pos++; // exactly one whitespace byte separates the header from the raw bit data

        var stride = (width + 7) / 8;
        var rows = new byte[height * stride];
        Array.Copy(data, pos, rows, 0, Math.Min(rows.Length, data.Length - pos));

        return new ImageElement(rows, width, height, justification);
    }

    private static string ReadToken(byte[] data, ref int pos)
    {
        while (pos < data.Length && char.IsWhiteSpace((char)data[pos]))
            pos++;

        if (pos < data.Length && data[pos] == (byte)'#')
        {
            while (pos < data.Length && data[pos] != (byte)'\n')
                pos++;
            return ReadToken(data, ref pos);
        }

        var start = pos;
        while (pos < data.Length && !char.IsWhiteSpace((char)data[pos]))
            pos++;

        return Encoding.ASCII.GetString(data, start, pos - start);
    }
}
