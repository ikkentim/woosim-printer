using System.Globalization;

namespace ReceiptPrinter;

public sealed class CalendarWidget : IBriefingWidget
{
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

    public async Task RenderAsync(IReceiptPrinter printer)
    {
        var (today, upcoming) = await LoadAsync();

        printer.SetBold(true);
        printer.Line("VANDAAG");
        printer.SetBold(false);
        if (today.Count == 0)
        {
            printer.Line("(geen afspraken vandaag)");
        }
        else
        {
            foreach (var ev in today)
                printer.Line($"- {(ev.AllDay ? "Hele dag" : ev.Start.ToString("HH:mm"))}  {ev.Summary}");
        }
        printer.Feed(1);

        printer.SetBold(true);
        printer.Line("AANKOMEND (komende 14 dagen)");
        printer.SetBold(false);
        if (upcoming.Count == 0)
        {
            printer.Line("(niets aankomend)");
        }
        else
        {
            foreach (var ev in upcoming.Take(3))
                printer.Line($"- {ev.Start.ToString("ddd d MMM", Dutch)}  {ev.Summary}");
        }
        printer.Feed(1);
    }

    private static async Task<(List<CalendarEventInfo> Today, List<CalendarEventInfo> Upcoming)> LoadAsync()
    {
        var haConfig = BriefingConfig.LoadHa();
        if (haConfig == null)
            return (new List<CalendarEventInfo>(), new List<CalendarEventInfo>());

        try
        {
            var start = DateTime.Today;
            var end = start.AddDays(14);
            var events = await HomeAssistantCalendar.GetEventsAsync(haConfig.BaseUrl, haConfig.Token, start, end);

            var today = events.Where(e => e.Start.Date == start).ToList();
            var upcoming = events.Where(e => e.Start.Date > start).ToList();
            return (today, upcoming);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Home Assistant calendar fetch failed: {ex}");
            return (new List<CalendarEventInfo>(), new List<CalendarEventInfo>());
        }
    }
}
