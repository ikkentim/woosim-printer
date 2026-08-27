using System.Globalization;
using System.Text.Json;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

public sealed class WeatherWidget(HomeAssistantOptions homeAssistant) : IBriefingWidget
{
    // Falls back to this only if Home Assistant's own location can't be reached at all - keeps the
    // widget useful (with a nudge in the log) rather than just going blank.
    private const double FallbackLatitude = 52.5546;
    private const double FallbackLongitude = 5.9114;

    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var (lat, lon) = await GetLocationAsync();
        var weather = await GetWeatherAsync(lat, lon);

        return
        [
            new TextElement(new string('-', 32)),
            new TextElement(weather ?? Localization.T("weather.unavailable")),
            new TextElement(new string('-', 32)),
            new TextElement(""),
        ];
    }

    private async Task<(double Latitude, double Longitude)> GetLocationAsync()
    {
        var connection = HomeAssistantConnection.Resolve(homeAssistant);
        if (connection == null)
        {
            Console.WriteLine("No Home Assistant connection available for location - using fallback coordinates");
            return (FallbackLatitude, FallbackLongitude);
        }

        try
        {
            var location = await HomeAssistantLocation.GetAsync(connection.RestBaseUrl, connection.Token);
            if (location != null)
                return location.Value;

            Console.WriteLine("Home Assistant's /api/config had no latitude/longitude - using fallback coordinates");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Home Assistant location fetch failed, using fallback coordinates: {ex}");
        }

        return (FallbackLatitude, FallbackLongitude);
    }

    private static async Task<string?> GetWeatherAsync(double latitude, double longitude)
    {
        try
        {
            using var http = new HttpClient();
            var lat = latitude.ToString(CultureInfo.InvariantCulture);
            var lon = longitude.ToString(CultureInfo.InvariantCulture);
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                      "&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min" +
                      "&timezone=auto";
            var json = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var current = doc.RootElement.GetProperty("current");
            var temp = current.GetProperty("temperature_2m").GetDouble();
            var code = current.GetProperty("weather_code").GetInt32();

            var daily = doc.RootElement.GetProperty("daily");
            var tMax = daily.GetProperty("temperature_2m_max")[0].GetDouble();
            var tMin = daily.GetProperty("temperature_2m_min")[0].GetDouble();

            return $"{DescribeWeather(code)}, {temp:0.#}C {Localization.T("weather.now")}\n" +
                   $"{Localization.T("weather.max")}:{tMax:0.#}C {Localization.T("weather.min")}:{tMin:0.#}C";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Weather fetch failed: {ex}");
            return null;
        }
    }

    private static string DescribeWeather(int code) => code switch
    {
        0 => Localization.T("weather.clear"),
        1 or 2 or 3 => Localization.T("weather.partly_cloudy"),
        45 or 48 => Localization.T("weather.fog"),
        51 or 53 or 55 => Localization.T("weather.drizzle"),
        61 or 63 or 65 => Localization.T("weather.rain"),
        71 or 73 or 75 => Localization.T("weather.snow"),
        80 or 81 or 82 => Localization.T("weather.showers"),
        95 or 96 or 99 => Localization.T("weather.thunder"),
        _ => Localization.T("weather.unknown"),
    };
}
