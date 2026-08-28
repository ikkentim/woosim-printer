using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// Supplementary current-weather detail lines - today's high / low, humidity and wind - in a
/// left/right split layout on the fixed-width receipt. Deliberately does <em>not</em> repeat the
/// condition and current temperature: those are <see cref="WeatherIconWidget"/>'s job (and
/// precipitation is <see cref="HourlyRainWidget"/>).
/// </summary>
public sealed class WeatherWidget(HomeAssistantOptions homeAssistant) : IBriefingWidget
{
    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var weather = await CurrentWeather.GetAsync(homeAssistant);
        if (weather == null)
            return [new TextElement(Localization.T("weather.unavailable"))];

        var lines = new List<string>();

        if (weather.High is { } hi && weather.Low is { } lo)
            lines.Add(WidgetLayout.SplitLine(
                $"{Localization.T("weather.max")} {hi:0}°C",
                $"{Localization.T("weather.min")} {lo:0}°C"));

        var humidity = weather.Humidity is { } h ? $"{Localization.T("weather.humidity")} {h:0}%" : "";
        var wind = weather.WindSpeed is { } w ? $"{Localization.T("weather.wind")} {w:0} {weather.WindUnit}".TrimEnd() : "";
        if (humidity.Length > 0 || wind.Length > 0)
            lines.Add(WidgetLayout.SplitLine(humidity, wind));

        if (lines.Count == 0)
            return Array.Empty<IElement>();

        var elements = new List<IElement> { new TextElement(WidgetLayout.Divider()) };
        elements.AddRange(lines.Select(l => new TextElement(l)));
        elements.Add(new TextElement(WidgetLayout.Divider()));
        elements.Add(new TextElement(""));

        return elements;
    }
}
