namespace ReceiptPrinter.Receipts;

/// <summary>
/// A line (or run) of text on a receipt, fully describing its own formatting - each element is
/// self-contained, so a printer never needs to track "current" bold/size/justification state.
/// </summary>
public record TextElement(
    string Text,
    bool LineBreak = true,
    bool Bold = false,
    bool Underline = false,
    int Width = 1,
    int Height = 1,
    Justification Justification = Justification.Left
) : IElement;
