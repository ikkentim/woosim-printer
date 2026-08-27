using ReceiptPrinter.Configuration;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// Builds the daily briefing receipt by running each configured widget in order and collecting their
/// elements. Which widgets run, in what order, and in what language, comes from ReceiptPrinterOptions.Briefing
/// - falls back to every widget in the original order.
/// </summary>
public static class DailyBriefing
{
    public static async Task<Receipt> BuildAsync(ReceiptPrinterOptions options)
    {
        Localization.SetLanguage(options.Briefing.Language);

        var widgetFactories = new Dictionary<string, Func<IBriefingWidget>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DateHeader"] = () => new DateHeaderWidget(),
            ["Weather"] = () => new WeatherWidget(options.HomeAssistant),
            ["Calendar"] = () => new CalendarWidget(options.HomeAssistant),
            ["Todo"] = () => new TodoWidget(options.HomeAssistant),
            ["Energy"] = () => new EnergyWidget(options.HomeAssistant),
        };

        var order = options.Briefing.Widgets is { Count: > 0 } ? options.Briefing.Widgets : BriefingOptions.DefaultWidgetOrder;

        var elements = new List<IElement>();
        foreach (var name in order)
        {
            if (!widgetFactories.TryGetValue(name, out var factory))
            {
                Console.WriteLine($"Unknown briefing widget '{name}' in Briefing:Widgets, skipping.");
                continue;
            }

            elements.AddRange(await factory().RenderAsync());
        }

        // A little extra breathing room before the cut.
        elements.Add(new TextElement(""));
        elements.Add(new TextElement(""));
        elements.Add(new TextElement(""));
        elements.Add(new TextElement(""));

        return new Receipt(elements, CutStyle.Partial);
    }
}
