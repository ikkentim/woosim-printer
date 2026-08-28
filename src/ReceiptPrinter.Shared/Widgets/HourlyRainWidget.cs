using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// Today's daylight-hours precipitation as a rasterised bar chart (one bar per hour, mm on the Y
/// axis), with each rain peak's mm value printed above its bar, an hour scale below, and a day total.
/// A dry day collapses to a single text line. Renders nothing when there's no hourly forecast, so it's
/// safe to leave in the widget list.
/// </summary>
public sealed class HourlyRainWidget(HomeAssistantOptions homeAssistant) : IBriefingWidget
{
    private const double WetThresholdMm = 0.05; // below this an hour rounds to 0.0 mm - treat as dry
    private const int Columns = 42;             // characters per line on this printer
    private const int CharDots = 9;             // ...at 9 dots each
    private const int ChartWidth = Columns * CharDots; // keep the chart the same width as the label rows
    private const int ChartHeight = 56;
    private const int HourLabelStep = 2;

    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var hours = await LoadAsync();
        if (hours.Count == 0)
            return Array.Empty<IElement>();

        var heading = new TextElement(Localization.T("weather.rain_heading"), Bold: true);
        var mm = hours.Select(h => h.Precipitation ?? 0).ToList();

        if (mm.All(v => v < WetThresholdMm))
            return [heading, new TextElement(Localization.T("weather.rain_dry")), new TextElement("")];

        return
        [
            heading,
            new TextElement(PeakLabelRow(mm)),
            WeatherChart.Bars(mm, ChartWidth, ChartHeight),
            new TextElement(HourScaleRow(hours)),
            new TextElement(string.Format(Localization.T("weather.rain_total"), mm.Sum().ToString("0.0", Localization.Culture))),
            new TextElement(""),
        ];
    }

    /// <summary>A line with each rain peak's mm value aligned to the left edge of its bar.</summary>
    private static string PeakLabelRow(IReadOnlyList<double> mm)
    {
        var line = new char[Columns];
        Array.Fill(line, ' ');
        var writtenTo = -1;

        for (var i = 0; i < mm.Count; i++)
        {
            var left = i > 0 ? mm[i - 1] : 0;
            var right = i < mm.Count - 1 ? mm[i + 1] : 0;
            if (mm[i] < WetThresholdMm || mm[i] <= left || mm[i] < right)
                continue; // not a local peak

            // Left-aligned to the bar's left edge - reads more clearly than centring over a 1-dot-wide peak.
            var text = mm[i].ToString("0.0", Localization.Culture);
            var start = Math.Clamp(i * Columns / mm.Count, 0, Columns - text.Length);
            if (start <= writtenTo)
                continue; // would overlap the previous label

            for (var k = 0; k < text.Length; k++)
                line[start + k] = text[k];
            writtenTo = start + text.Length;
        }

        return new string(line).TrimEnd();
    }

    private static string HourScaleRow(IReadOnlyList<HourlyForecastPoint> hours)
    {
        var ticks = new List<int>();
        for (var i = 0; i < hours.Count; i += HourLabelStep)
            ticks.Add(i);
        if (ticks.Count > 0 && ticks[^1] != hours.Count - 1)
            ticks[^1] = hours.Count - 1; // pull the last tick onto the final hour so the axis end is marked

        var line = new char[Columns];
        Array.Fill(line, ' ');

        foreach (var i in ticks)
        {
            var text = $"{hours[i].Time.Hour:00}";
            var start = Math.Clamp(BarCentreChar(i, hours.Count) - text.Length / 2, 0, Columns - text.Length);
            for (var k = 0; k < text.Length; k++)
                line[start + k] = text[k];
        }

        return new string(line).TrimEnd();
    }

    /// <summary>The character column under the centre of bar <paramref name="i"/> of <paramref name="count"/>.</summary>
    private static int BarCentreChar(int i, int count) => (int)Math.Round((i + 0.5) * Columns / count);

    private async Task<IReadOnlyList<HourlyForecastPoint>> LoadAsync()
    {
        var connection = HomeAssistantConnection.Resolve(homeAssistant);
        if (connection == null)
            return Array.Empty<HourlyForecastPoint>();

        try
        {
            var snapshot = await HomeAssistantWeather.GetSnapshotAsync(connection.RestBaseUrl, connection.Token);
            return snapshot.DaytimeHours();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hourly rain fetch failed: {ex}");
            return Array.Empty<HourlyForecastPoint>();
        }
    }
}
