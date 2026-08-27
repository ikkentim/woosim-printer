using ReceiptPrinter.Configuration;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// Top-level entry point for "print the daily briefing" (the scheduled/on-demand trigger, not a widget
/// itself) - sets the language, runs <see cref="DailyBriefingWidget"/>, and wraps its elements as a
/// printable <see cref="Receipt"/>. The actual widget-combining logic lives in DailyBriefingWidget.
/// </summary>
public static class DailyBriefing
{
    public static async Task<Receipt> BuildAsync(ReceiptPrinterOptions options)
    {
        Localization.SetLanguage(options.Briefing.Language);

        var elements = await new DailyBriefingWidget(options).RenderAsync();
        return new Receipt(elements, CutStyle.Partial);
    }
}
