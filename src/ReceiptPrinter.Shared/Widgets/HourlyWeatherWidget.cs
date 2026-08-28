using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// A compact "temperature by hour" strip - the next ~12 hours sampled every 3 hours - from Home
/// Assistant's auto-discovered weather entity. Renders nothing if there's no hourly forecast available
/// (no weather entity, or one that only does daily), so it's safe to leave in the widget list.
/// </summary>
public sealed class HourlyWeatherWidget(HomeAssistantOptions homeAssistant) : IBriefingWidget
{
    private const int WindowHours = 15;
    private const int StepHours = 3;
    private const int PointsPerLine = 3;

    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var points = await LoadAsync();

        var labels = new List<string>();
        for (var i = 0; i < points.Count && i <= WindowHours - StepHours; i += StepHours)
        {
            if (points[i].Temperature is { } temp)
                labels.Add($"{points[i].Time.Hour:00}u {temp:0}C");
        }

        if (labels.Count == 0)
            return Array.Empty<IElement>();

        var elements = new List<IElement> { new TextElement(Localization.T("weather.hourly_heading"), Bold: true) };
        foreach (var line in labels.Chunk(PointsPerLine))
            elements.Add(new TextElement(string.Join("  ", line)));
        elements.Add(new TextElement(""));

        return elements;
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
            Console.WriteLine($"Hourly weather fetch failed: {ex}");
            return Array.Empty<HourlyForecastPoint>();
        }
    }
}
