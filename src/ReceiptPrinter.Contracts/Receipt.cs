namespace ReceiptPrinter;

/// <summary>
/// A complete receipt ready to print: its content, and how to cut the paper afterwards.
/// </summary>
public record Receipt(IReadOnlyList<IElement> Elements, CutStyle Cut = CutStyle.Full);
