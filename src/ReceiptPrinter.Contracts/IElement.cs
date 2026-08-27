using System.Text.Json.Serialization;

namespace ReceiptPrinter;

/// <summary>
/// A single piece of content on a receipt. Printer implementations translate elements into whatever
/// their transport needs (ESC/POS bytes over serial, an HTTP payload over the network, etc.) - callers
/// building a <see cref="Receipt"/> never need to know which.
/// </summary>
/// <remarks>
/// Marked polymorphic so a <see cref="Receipt"/> can round-trip through JSON (e.g. the Service's
/// POST /print endpoint) - add a <see cref="JsonDerivedTypeAttribute"/> here for each new element type.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement), "text")]
public interface IElement
{
}
