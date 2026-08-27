using ReceiptPrinter.Configuration;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// Combines whichever widgets <see cref="BriefingOptions.Widgets"/> configures, in that order, into one
/// - this is what makes "the daily briefing" a briefing at all, rather than a single fixed widget. It's
/// itself an <see cref="IBriefingWidget"/> so it composes the same way everything else does - e.g. it's
/// referenceable as "[DailyBriefing]" from <see cref="Receipts.ReceiptMarkdown"/>, same as any other
/// widget (though listing "DailyBriefing" inside its own Briefing:Widgets would obviously misbehave -
/// nothing guards against that self-reference, it's just not a sensible thing to configure).
/// </summary>
public sealed class DailyBriefingWidget(ReceiptPrinterOptions options) : IBriefingWidget
{
    /// <summary>Every widget nameable in Briefing:Widgets (or "[Name]" in ReceiptMarkdown), keyed
    /// case-insensitively, including this composite widget itself under "DailyBriefing".</summary>
    public static Dictionary<string, Func<IBriefingWidget>> CreateWidgetFactories(ReceiptPrinterOptions options) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DateHeader"] = () => new DateHeaderWidget(),
            ["Weather"] = () => new WeatherWidget(options.HomeAssistant),
            ["Calendar"] = () => new CalendarWidget(options.HomeAssistant),
            ["Todo"] = () => new TodoWidget(options.HomeAssistant),
            ["Energy"] = () => new EnergyWidget(options.HomeAssistant),
            ["DailyBriefing"] = () => new DailyBriefingWidget(options),
        };

    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var widgetFactories = CreateWidgetFactories(options);
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

        return elements;
    }
}
