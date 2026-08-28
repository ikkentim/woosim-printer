using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReceiptPrinter.HomeAssistant;

/// <summary>One hour of forecast: local time, temperature (C), and precipitation for that hour (mm).</summary>
public record HourlyForecastPoint(DateTimeOffset Time, double? Temperature, double? Precipitation);

/// <summary>
/// Everything the weather widgets need from Home Assistant in one shot: current conditions, today's
/// high/low, the hourly forecast, and today's sunrise/sunset (from <c>sun.sun</c>, null if that entity
/// isn't present). <see cref="Condition"/> is null when there's no usable weather entity (the
/// current-conditions widget then falls back to open-meteo).
/// </summary>
public record WeatherSnapshot(
    string? Condition,
    double? Temperature,
    double? TempHigh,
    double? TempLow,
    IReadOnlyList<HourlyForecastPoint> Hourly,
    DateTimeOffset? SunriseToday,
    DateTimeOffset? SunsetToday)
{
    /// <summary>
    /// The hourly points that fall within today's daylight - from the hour containing sunrise through
    /// the hour containing sunset. Falls back to every remaining hour of today when sun times are
    /// unknown (no <c>sun.sun</c> entity).
    /// </summary>
    public IReadOnlyList<HourlyForecastPoint> DaytimeHours()
    {
        var today = DateTimeOffset.Now.Date;
        var fromHour = SunriseToday?.Hour ?? 0;
        var toHour = SunsetToday?.Hour ?? 23;

        return Hourly
            .Where(p => p.Time.Date == today && p.Time.Hour >= fromHour && p.Time.Hour <= toHour)
            .ToArray();
    }
}

/// <summary>
/// Reads weather from Home Assistant's own weather integration rather than a third-party API:
/// auto-discovers the first <c>weather.*</c> entity, then reads its state/attributes and the
/// <c>weather.get_forecasts</c> service (modern Home Assistant no longer exposes a <c>forecast</c>
/// attribute directly). One <see cref="GetSnapshotAsync"/> call does every request; the result is
/// briefly cached so the three weather widgets in a briefing share it instead of each re-fetching.
/// </summary>
public static class HomeAssistantWeather
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(90);
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static string? _cacheKey;
    private static DateTime _cachedAtUtc;
    private static WeatherSnapshot? _cached;

    public static async Task<WeatherSnapshot> GetSnapshotAsync(string restBaseUrl, string token)
    {
        var baseUrl = restBaseUrl.TrimEnd('/');

        await CacheLock.WaitAsync();
        try
        {
            if (_cached != null && _cacheKey == baseUrl && DateTime.UtcNow - _cachedAtUtc < CacheTtl)
                return _cached;

            _cached = await FetchAsync(baseUrl, token);
            _cacheKey = baseUrl;
            _cachedAtUtc = DateTime.UtcNow;
            return _cached;
        }
        finally
        {
            CacheLock.Release();
        }
    }

    private static async Task<WeatherSnapshot> FetchAsync(string baseUrl, string token)
    {
        using var http = CreateClient(token);

        var (sunrise, sunset) = await GetSunTimesAsync(http, baseUrl);

        var entityId = await FindWeatherEntityAsync(http, baseUrl);
        if (entityId == null)
        {
            Console.WriteLine("No weather.* entity found in Home Assistant");
            return new WeatherSnapshot(null, null, null, null, Array.Empty<HourlyForecastPoint>(), sunrise, sunset);
        }

        string? condition = null;
        double? temperature = null;
        try
        {
            var stateJson = await http.GetStringAsync($"{baseUrl}/api/states/{entityId}");
            using var stateDoc = JsonDocument.Parse(stateJson);
            var root = stateDoc.RootElement;

            condition = root.GetProperty("state").GetString();
            if (root.TryGetProperty("attributes", out var attrs))
                temperature = GetNumber(attrs, "temperature");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Home Assistant weather state fetch failed: {ex}");
        }

        var (high, low) = await GetTodayHighLowAsync(http, baseUrl, entityId);
        var hourly = await GetHourlyAsync(http, baseUrl, entityId);

        return new WeatherSnapshot(condition, temperature, high, low, hourly, sunrise, sunset);
    }

    /// <summary>
    /// Today's sunrise/sunset from <c>sun.sun</c>. The entity only exposes the <em>next</em> events, so
    /// one of them belongs to tomorrow depending on the time of day - that one comes back null.
    /// </summary>
    private static async Task<(DateTimeOffset? Sunrise, DateTimeOffset? Sunset)> GetSunTimesAsync(HttpClient http, string baseUrl)
    {
        try
        {
            var json = await http.GetStringAsync($"{baseUrl}/api/states/sun.sun");
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("attributes", out var attrs))
                return (null, null);

            var today = DateTimeOffset.Now.Date;
            var rising = ParseTime(attrs, "next_rising");
            var setting = ParseTime(attrs, "next_setting");

            return (rising?.Date == today ? rising : null, setting?.Date == today ? setting : null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Home Assistant sun.sun fetch failed: {ex}");
            return (null, null);
        }
    }

    private static DateTimeOffset? ParseTime(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.GetString() is { } s && DateTimeOffset.TryParse(s, out var dt)
            ? dt.ToLocalTime()
            : null;

    private static async Task<IReadOnlyList<HourlyForecastPoint>> GetHourlyAsync(HttpClient http, string baseUrl, string entityId)
    {
        var json = await GetForecastsAsync(http, baseUrl, entityId, "hourly");
        if (json == null)
            return Array.Empty<HourlyForecastPoint>();

        using var doc = JsonDocument.Parse(json);
        if (!TryGetForecastArray(doc, entityId, out var forecast))
            return Array.Empty<HourlyForecastPoint>();

        var cutoff = DateTimeOffset.Now.AddHours(-1);

        return forecast.EnumerateArray()
            .Select(e => new HourlyForecastPoint(
                e.TryGetProperty("datetime", out var dt) && dt.GetString() is { } s
                    ? DateTimeOffset.Parse(s).ToLocalTime()
                    : default,
                GetNumber(e, "temperature"),
                GetNumber(e, "precipitation")))
            .Where(p => p.Time > cutoff)
            .Take(24)
            .ToArray();
    }

    /// <summary>
    /// Today's daily bucket. Best-effort: an entity with no daily forecast just yields no high/low,
    /// and the caller still shows the current conditions.
    /// </summary>
    private static async Task<(double? High, double? Low)> GetTodayHighLowAsync(HttpClient http, string baseUrl, string entityId)
    {
        var json = await GetForecastsAsync(http, baseUrl, entityId, "daily");
        if (json == null)
            return (null, null);

        using var doc = JsonDocument.Parse(json);
        if (!TryGetForecastArray(doc, entityId, out var forecast) || forecast.GetArrayLength() == 0)
            return (null, null);

        var today = forecast[0];
        return (GetNumber(today, "temperature"), GetNumber(today, "templow"));
    }

    private static HttpClient CreateClient(string token)
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    private static async Task<string?> FindWeatherEntityAsync(HttpClient http, string baseUrl)
    {
        var json = await http.GetStringAsync($"{baseUrl}/api/states");
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.EnumerateArray()
            .Select(e => e.TryGetProperty("entity_id", out var id) ? id.GetString() : null)
            .FirstOrDefault(id => id != null && id.StartsWith("weather.", StringComparison.Ordinal));
    }

    private static async Task<string?> GetForecastsAsync(HttpClient http, string baseUrl, string entityId, string type)
    {
        try
        {
            using var body = new StringContent(
                JsonSerializer.Serialize(new { entity_id = entityId, type }),
                Encoding.UTF8, "application/json");

            using var response = await http.PostAsync($"{baseUrl}/api/services/weather/get_forecasts?return_response", body);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Home Assistant weather.get_forecasts ({type}) failed: {ex}");
            return null;
        }
    }

    private static bool TryGetForecastArray(JsonDocument doc, string entityId, out JsonElement forecast)
    {
        forecast = default;
        return doc.RootElement.TryGetProperty("service_response", out var response)
            && response.TryGetProperty(entityId, out var entity)
            && entity.TryGetProperty("forecast", out forecast)
            && forecast.ValueKind == JsonValueKind.Array;
    }

    private static double? GetNumber(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
}
