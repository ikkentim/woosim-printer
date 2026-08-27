namespace ReceiptPrinter;

/// <summary>
/// A self-contained section of the daily briefing receipt. Each widget fetches whatever data it needs
/// and produces the elements for its own section - it never touches a printer directly, so widgets can
/// be composed into a <see cref="Receipt"/> and sent to whichever printer implementation is in use.
/// </summary>
public interface IBriefingWidget
{
    Task<IReadOnlyList<IElement>> RenderAsync();
}
