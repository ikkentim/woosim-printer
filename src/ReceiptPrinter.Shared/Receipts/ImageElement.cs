namespace ReceiptPrinter.Receipts;

/// <summary>
/// A 1-bit-per-pixel raster image on a receipt, printed via the ESC/POS bit-image command. Rows are
/// packed MSB-first with each row padded to a whole byte (the same layout as a binary PBM "P4" file);
/// a set bit prints a dot. <see cref="Width"/> is in pixels, so <c>Rows.Length == Height * ceil(Width/8)</c>.
/// </summary>
public sealed record ImageElement(
    byte[] Rows,
    int Width,
    int Height,
    Justification Justification = Justification.Left
) : IElement;
