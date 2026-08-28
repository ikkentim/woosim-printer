using System.Text.Json.Serialization;

namespace ReceiptPrinter.Receipts;

/// <summary>
/// A single piece of content on a receipt. Printer implementations translate elements into whatever
/// their transport needs (ESC/POS bytes over serial, an HTTP payload over the network, etc.) - callers
/// building a <see cref="Receipt"/> never need to know which.
/// </summary>
/// <remarks>
/// Marked polymorphic so a <see cref="Receipt"/> can still round-trip through JSON if needed (nothing
/// on the print path does today - it goes out as raw ESC/POS bytes via <c>EscPosEncoder</c>) - add a
/// <see cref="JsonDerivedTypeAttribute"/> here for each new element type.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement), "text")]
public interface IElement
{
}
