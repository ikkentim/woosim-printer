using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// The weather glyph for the current conditions, centred, with the condition and temperature under
/// it. Pairs with <see cref="WeatherWidget"/> (the detail lines) and <see cref="HourlyRainWidget"/>,
/// but the three can be ordered independently in <c>Briefing.Widgets</c>.
/// </summary>
public sealed class WeatherIconWidget(HomeAssistantOptions homeAssistant) : IBriefingWidget
{
    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var weather = await CurrentWeather.GetAsync(homeAssistant);
        if (weather == null)
            return [new TextElement(Localization.T("weather.unavailable"), Justification: Justification.Center)];

        var headline = weather.Temperature is { } t
            ? $"{weather.Description}  {t:0}°C"
            : weather.Description;

        var elements = new List<IElement>();
        if (WeatherIcon.ForCondition(weather.Condition) is { } icon)
            elements.Add(icon);
        elements.Add(new TextElement(headline, Bold: true, Justification: Justification.Center));
        elements.Add(new TextElement(""));

        return elements;
    }
}
