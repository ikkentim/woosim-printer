using System.Globalization;
using System.Text.Json;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Printers;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

/// <summary>
/// Prints current conditions plus today's high/low. Primary source is Home Assistant's own weather
/// integration (auto-discovered <c>weather.*</c> entity) - consistent with the other widgets and needing
/// no extra config. If Home Assistant isn't reachable or has no weather entity, it falls back to
/// open-meteo, using the latitude/longitude Home Assistant already has configured.
/// </summary>
public sealed class WeatherWidget(HomeAssistantOptions homeAssistant) : IBriefingWidget
{
    // Falls back to this only if Home Assistant's own location can't be reached at all - keeps the
    // widget useful (with a nudge in the log) rather than just going blank.
    private const double FallbackLatitude = 52.5546;
    private const double FallbackLongitude = 5.9114;

    /// <summary>A rendered weather line, plus the Home Assistant condition key for its printed icon.</summary>
    private sealed record WeatherRender(string Text, string? Condition);

    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var weather = await GetFromHomeAssistantAsync() ?? await GetFromOpenMeteoAsync();

        var elements = new List<IElement> { new TextElement(new string('-', 32)) };
        if (WeatherIcon.ForCondition(weather?.Condition) is { } icon)
            elements.Add(icon);
        elements.Add(new TextElement(weather?.Text ?? Localization.T("weather.unavailable")));
        elements.Add(new TextElement(new string('-', 32)));
        elements.Add(new TextElement(""));

        return elements;
    }

    private async Task<WeatherRender?> GetFromHomeAssistantAsync()
    {
        var connection = HomeAssistantConnection.Resolve(homeAssistant);
        if (connection == null)
            return null;

        try
        {
            var reading = await HomeAssistantWeather.GetAsync(connection.RestBaseUrl, connection.Token);
            if (reading == null)
            {
                Console.WriteLine("No weather.* entity found in Home Assistant - falling back to open-meteo");
                return null;
            }

            var text = Format(DescribeCondition(reading.Condition), reading.Temperature, reading.TempHigh, reading.TempLow);
            return new WeatherRender(text, reading.Condition);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Home Assistant weather fetch failed, falling back to open-meteo: {ex}");
            return null;
        }
    }

    private async Task<WeatherRender?> GetFromOpenMeteoAsync()
    {
        var (lat, lon) = await GetLocationAsync();

        try
        {
            using var http = new HttpClient();
            var latText = lat.ToString(CultureInfo.InvariantCulture);
            var lonText = lon.ToString(CultureInfo.InvariantCulture);
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latText}&longitude={lonText}" +
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

            return new WeatherRender(Format(DescribeWeatherCode(code), temp, tMax, tMin), WmoToCondition(code));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Weather fetch failed: {ex}");
            return null;
        }
    }

    private static string Format(string description, double? temp, double? high, double? low)
    {
        var line = temp is { } t
            ? $"{description}, {t:0.#}C {Localization.T("weather.now")}"
            : description;

        if (high is { } hi && low is { } lo)
            line += $"\n{Localization.T("weather.max")}:{hi:0.#}C {Localization.T("weather.min")}:{lo:0.#}C";

        return line;
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

    /// <summary>Maps a Home Assistant weather condition (entity state) to a localized description.</summary>
    private static string DescribeCondition(string condition) => condition switch
    {
        "sunny" or "clear-night" => Localization.T("weather.clear"),
        "partlycloudy" => Localization.T("weather.partly_cloudy"),
        "cloudy" => Localization.T("weather.cloudy"),
        "fog" => Localization.T("weather.fog"),
        "rainy" or "pouring" => Localization.T("weather.rain"),
        "snowy" or "snowy-rainy" or "hail" => Localization.T("weather.snow"),
        "lightning" or "lightning-rainy" => Localization.T("weather.thunder"),
        "windy" or "windy-variant" => Localization.T("weather.windy"),
        _ => Localization.T("weather.unknown"),
    };

    /// <summary>Maps an open-meteo WMO weather code to a localized description.</summary>
    private static string DescribeWeatherCode(int code) => code switch
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

    /// <summary>Maps an open-meteo WMO weather code to a Home Assistant condition, for icon lookup.</summary>
    private static string? WmoToCondition(int code) => code switch
    {
        0 => "sunny",
        1 or 2 => "partlycloudy",
        3 => "cloudy",
        45 or 48 => "fog",
        51 or 53 or 55 or 56 or 57 or 61 or 63 => "rainy",
        65 or 66 or 67 or 80 or 81 or 82 => "pouring",
        71 or 73 or 75 or 77 or 85 or 86 => "snowy",
        95 or 96 or 99 => "lightning",
        _ => null,
    };
}
