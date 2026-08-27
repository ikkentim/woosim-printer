namespace ReceiptPrinter;

/// <summary>
/// Prints the daily briefing receipt by running each widget in order.
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

    public static async Task PrintAsync(IReceiptPrinter printer)
    {
        foreach (var widget in Widgets)
            await widget.RenderAsync(printer);

        printer.Line();
        printer.Feed(3);
        printer.CutPaper();
    }
}
