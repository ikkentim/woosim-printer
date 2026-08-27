using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

public sealed class CalendarWidget : IBriefingWidget
{
    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var (today, upcoming) = await LoadAsync();
        var elements = new List<IElement>();

        elements.Add(new TextElement(Localization.T("calendar.today"), Bold: true));
        if (today.Count == 0)
        {
            elements.Add(new TextElement(Localization.T("calendar.none_today")));
        }
        else
        {
            foreach (var ev in today)
                elements.Add(new TextElement($"- {(ev.AllDay ? Localization.T("calendar.all_day") : ev.Start.ToString("HH:mm"))}  {ev.Summary}"));
        }
        elements.Add(new TextElement(""));

        elements.Add(new TextElement(Localization.T("calendar.upcoming"), Bold: true));
        if (upcoming.Count == 0)
        {
            elements.Add(new TextElement(Localization.T("calendar.none_upcoming")));
        }
        else
        {
            foreach (var ev in upcoming.Take(3))
                elements.Add(new TextElement($"- {ev.Start.ToString("ddd d MMM", Localization.Culture)}  {ev.Summary}"));
        }
        elements.Add(new TextElement(""));

        return elements;
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
