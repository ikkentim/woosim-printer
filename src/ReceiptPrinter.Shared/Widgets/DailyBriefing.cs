using ReceiptPrinter.Configuration;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// Builds the daily briefing receipt by running each configured widget in order and collecting their
/// elements. Which widgets run, in what order, and in what language, comes from briefing-settings.json
/// (see <see cref="BriefingConfig.LoadSettings"/>) - falls back to every widget in the original order.
/// </summary>
public static class DailyBriefing
{
    private static readonly IReadOnlyDictionary<string, Func<IBriefingWidget>> WidgetFactories =
        new Dictionary<string, Func<IBriefingWidget>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DateHeader"] = () => new DateHeaderWidget(),
            ["Weather"] = () => new WeatherWidget(),
            ["Calendar"] = () => new CalendarWidget(),
            ["Todo"] = () => new TodoWidget(),
            ["Energy"] = () => new EnergyWidget(),
        };

    public static async Task<Receipt> BuildAsync()
    {
        var settings = BriefingConfig.LoadSettings();
        Localization.SetLanguage(settings.Language);

        var order = settings.Widgets is { Count: > 0 } ? settings.Widgets : BriefingSettings.DefaultWidgetOrder;

        var elements = new List<IElement>();
        foreach (var name in order)
        {
            if (!WidgetFactories.TryGetValue(name, out var factory))
            {
                Console.WriteLine($"Unknown briefing widget '{name}' in briefing-settings.json, skipping.");
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
