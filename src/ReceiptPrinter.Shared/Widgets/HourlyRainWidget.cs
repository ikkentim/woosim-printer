using System.Text;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// A one-char-per-hour precipitation strip for the next 12 hours, from Home Assistant's auto-discovered
/// weather entity, with a plain-language summary line ("dry" / "rain ~10-13h"). Renders nothing if
/// there's no hourly forecast available, so it's safe to leave in the widget list.
/// </summary>
public sealed class HourlyRainWidget(HomeAssistantOptions homeAssistant) : IBriefingWidget
{
    private const int WindowHours = 12;
    private const double WetThresholdMm = 0.1;

    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var points = await LoadAsync();
        if (points.Count == 0)
            return Array.Empty<IElement>();

        var bar = new string(points.Select(p => IntensityChar(p.Precipitation ?? 0)).ToArray());

        // A time label every 3 hours, "HH " (3 chars) each, so they line up under the bar's columns.
        var axis = new StringBuilder(" ");
        for (var i = 0; i < points.Count; i += 3)
            axis.Append($"{points[i].Time.Hour:00} ");

        return
        [
            new TextElement(Localization.T("weather.rain_heading"), Bold: true),
            new TextElement($"|{bar}|"),
            new TextElement(axis.ToString().TrimEnd()),
            new TextElement(SummaryLine(points)),
            new TextElement(""),
        ];
    }

    private static char IntensityChar(double mm) => mm switch
    {
        < WetThresholdMm => '.',
        < 0.5 => ':',
        < 2.0 => '+',
        _ => '#',
    };

    private static string SummaryLine(IReadOnlyList<HourlyForecastPoint> points)
    {
        var wet = points.Where(p => (p.Precipitation ?? 0) >= WetThresholdMm).ToList();
        if (wet.Count == 0)
            return string.Format(Localization.T("weather.rain_dry"), points.Count);

        return string.Format(Localization.T("weather.rain_window"),
            $"{wet[0].Time.Hour:00}", $"{wet[^1].Time.Hour:00}");
    }

    private async Task<IReadOnlyList<HourlyForecastPoint>> LoadAsync()
    {
        var connection = HomeAssistantConnection.Resolve(homeAssistant);
        if (connection == null)
            return Array.Empty<HourlyForecastPoint>();

        try
        {
            return await HomeAssistantWeather.GetHourlyAsync(connection.RestBaseUrl, connection.Token, WindowHours);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hourly rain fetch failed: {ex}");
            return Array.Empty<HourlyForecastPoint>();
        }
    }
}
