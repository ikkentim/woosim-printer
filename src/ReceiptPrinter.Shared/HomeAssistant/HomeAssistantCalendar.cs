using System.Net.Http.Headers;
using System.Text.Json;

namespace ReceiptPrinter.HomeAssistant;

public record CalendarEventInfo(DateTime Start, bool AllDay, string Summary);

/// <summary>
/// Reads upcoming events from Home Assistant calendar entities (e.g. the iCloud caldav integration).
/// </summary>
public static class HomeAssistantCalendar
{
    public static async Task<List<CalendarEventInfo>> GetEventsAsync(string baseUrl, string token, DateTime start, DateTime end, IReadOnlyList<string>? entityIds = null)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        baseUrl = baseUrl.TrimEnd('/');

        var ids = entityIds != null && entityIds.Count > 0
            ? entityIds
            : await ListCalendarEntityIdsAsync(http, baseUrl);

        var startStr = Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        var endStr = Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:sszzz"));

        var results = new List<CalendarEventInfo>();
        foreach (var entityId in ids)
        {
            var url = $"{baseUrl}/api/calendars/{entityId}?start={startStr}&end={endStr}";
            try
            {
                var json = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                foreach (var ev in doc.RootElement.EnumerateArray())
                {
                    var summary = ev.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
                    var (startDt, allDay) = ParseEventTime(ev.GetProperty("start"));
                    results.Add(new CalendarEventInfo(startDt, allDay, summary));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Calendar fetch failed for {entityId}: {ex.Message}");
            }
        }

        return results.OrderBy(e => e.Start).ToList();
    }

    private static (DateTime, bool AllDay) ParseEventTime(JsonElement start)
    {
        // HA returns either a "date" (all-day) or "dateTime" property, or a plain string.
        if (start.ValueKind == JsonValueKind.Object)
        {
            if (start.TryGetProperty("dateTime", out var dt))
                return (DateTime.Parse(dt.GetString()!).ToLocalTime(), false);
            if (start.TryGetProperty("date", out var d))
                return (DateTime.Parse(d.GetString()!), true);
        }
        else if (start.ValueKind == JsonValueKind.String)
        {
            var value = start.GetString()!;
            if (value.Length <= 10)
                return (DateTime.Parse(value), true);
            return (DateTime.Parse(value).ToLocalTime(), false);
        }

        return (DateTime.MinValue, true);
    }

    private static async Task<List<string>> ListCalendarEntityIdsAsync(HttpClient http, string baseUrl)
    {
        var json = await http.GetStringAsync($"{baseUrl}/api/calendars");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("entity_id").GetString()!)
            .ToList();
    }
}
