namespace ReceiptPrinter;

/// <summary>
/// A self-contained section of the daily briefing receipt. Each widget fetches whatever data it needs
/// and renders its own section, so sections can be added, removed, or reordered independently.
/// </summary>
public interface IBriefingWidget
{
    Task RenderAsync(IReceiptPrinter printer);
}
