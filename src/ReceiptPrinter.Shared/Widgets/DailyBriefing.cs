using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// Builds the daily briefing receipt by running each widget in order and collecting their elements.
/// </summary>
public static class DailyBriefing
{
    private static readonly IBriefingWidget[] Widgets =
    {
        new DateHeaderWidget(),
        new WeatherWidget(),
        new CalendarWidget(),
        new TodoWidget(),
        new EnergyWidget(),
    };

    public static async Task<Receipt> BuildAsync()
    {
        var elements = new List<IElement>();
        foreach (var widget in Widgets)
            elements.AddRange(await widget.RenderAsync());

        // A little extra breathing room before the cut.
        elements.Add(new TextElement(""));
        elements.Add(new TextElement(""));
        elements.Add(new TextElement(""));
        elements.Add(new TextElement(""));

        return new Receipt(elements, CutStyle.Partial);
    }
}
