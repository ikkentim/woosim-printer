using System.Globalization;
using System.Text.Json;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;

namespace ReceiptPrinter.Widgets;

/// <summary>Current weather, resolved once and shared by the icon and detail widgets.</summary>
public sealed record CurrentConditions(
    string? Condition,      // Home Assistant condition key, for icon lookup (null if unknown)
    string Description,     // localized text, e.g. "Regen"
    double? Temperature,
    double? High,
    double? Low,
    double? Humidity,       // %
    double? WindSpeed,
    string? WindUnit);

/// <summary>
/// Resolves the current conditions from Home Assistant's auto-discovered <c>weather.*</c> entity,
/// falling back to open-meteo (using Home Assistant's configured location) when there's no such
/// entity. The result is cached briefly so the weather widgets in one briefing share a single lookup.
/// </summary>
public static class CurrentWeather
{
    private const double FallbackLatitude = 52.5546;
    private const double FallbackLongitude = 5.9114;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(90);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static DateTime _cachedAtUtc;
    private static CurrentConditions? _cached;
    private static bool _haveCached;

    public static async Task<CurrentConditions?> GetAsync(HomeAssistantOptions homeAssistant)
    {
        await CacheLock.WaitAsync();
        try
        {
            if (_haveCached && DateTime.UtcNow - _cachedAtUtc < CacheTtl)
                return _cached;

            _cached = await ResolveAsync(homeAssistant);
            _haveCached = true;
            _cachedAtUtc = DateTime.UtcNow;
            return _cached;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static async Task<CurrentConditions?> ResolveAsync(HomeAssistantOptions homeAssistant)
    {
        var connection = HomeAssistantConnection.Resolve(homeAssistant);

        if (connection != null)
        {
            try
            {
                var s = await HomeAssistantWeather.GetSnapshotAsync(connection.RestBaseUrl, connection.Token);
                if (s.Condition != null)
                    return new CurrentConditions(
                        s.Condition, DescribeCondition(s.Condition),
                        s.Temperature, s.TempHigh, s.TempLow, s.Humidity, s.WindSpeed, s.WindUnit);

                Console.WriteLine("No usable weather.* entity in Home Assistant - falling back to open-meteo");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Home Assistant weather fetch failed, falling back to open-meteo: {ex}");
            }
        }

        return await FromOpenMeteoAsync(homeAssistant, connection);
    }

    private static async Task<CurrentConditions?> FromOpenMeteoAsync(HomeAssistantOptions homeAssistant, HomeAssistantConnection? connection)
    {
        var (lat, lon) = await GetLocationAsync(connection);

        try
        {
            using var http = new HttpClient();
            var url = $"https://api.open-meteo.com/v1/forecast" +
                      $"?latitude={lat.ToString(CultureInfo.InvariantCulture)}" +
                      $"&longitude={lon.ToString(CultureInfo.InvariantCulture)}" +
                      "&current=temperature_2m,weather_code,relative_humidity_2m,wind_speed_10m" +
                      "&daily=temperature_2m_max,temperature_2m_min&timezone=auto";
            using var doc = JsonDocument.Parse(await http.GetStringAsync(url));

            var current = doc.RootElement.GetProperty("current");
            var code = current.GetProperty("weather_code").GetInt32();
            var daily = doc.RootElement.GetProperty("daily");

            return new CurrentConditions(
                WmoToCondition(code),
                DescribeWeatherCode(code),
                current.GetProperty("temperature_2m").GetDouble(),
                daily.GetProperty("temperature_2m_max")[0].GetDouble(),
                daily.GetProperty("temperature_2m_min")[0].GetDouble(),
                current.TryGetProperty("relative_humidity_2m", out var h) ? h.GetDouble() : null,
                current.TryGetProperty("wind_speed_10m", out var w) ? w.GetDouble() : null,
                "km/h");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Weather fetch failed: {ex}");
            return null;
        }
    }

    private static async Task<(double Latitude, double Longitude)> GetLocationAsync(HomeAssistantConnection? connection)
    {
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
