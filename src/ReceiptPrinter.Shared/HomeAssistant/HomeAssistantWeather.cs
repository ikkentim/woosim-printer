using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReceiptPrinter.HomeAssistant;

/// <summary>Current conditions plus today's high/low, as read from a Home Assistant weather entity.</summary>
public record HomeAssistantWeatherReading(string Condition, double? Temperature, double? TempHigh, double? TempLow);

/// <summary>One hour of forecast: local time, temperature (C), and precipitation for that hour (mm).</summary>
public record HourlyForecastPoint(DateTimeOffset Time, double? Temperature, double? Precipitation);

/// <summary>
/// Reads weather from Home Assistant's own weather integration rather than calling a third-party API:
/// auto-discovers the first <c>weather.*</c> entity, then reads its state/attributes and the
/// <c>weather.get_forecasts</c> service (modern Home Assistant no longer exposes a <c>forecast</c>
/// attribute directly).
/// </summary>
public static class HomeAssistantWeather
{
    /// <summary>Current condition + temperature, plus today's daily high/low.</summary>
    public static async Task<HomeAssistantWeatherReading?> GetAsync(string restBaseUrl, string token)
    {
        var baseUrl = restBaseUrl.TrimEnd('/');
        using var http = CreateClient(token);

        var entityId = await FindWeatherEntityAsync(http, baseUrl);
        if (entityId == null)
            return null;

        var stateJson = await http.GetStringAsync($"{baseUrl}/api/states/{entityId}");
        using var stateDoc = JsonDocument.Parse(stateJson);
        var root = stateDoc.RootElement;

        var condition = root.GetProperty("state").GetString() ?? "";
        double? temperature = root.TryGetProperty("attributes", out var attrs) ? GetNumber(attrs, "temperature") : null;

        var (high, low) = await GetTodayHighLowAsync(http, baseUrl, entityId);

        return new HomeAssistantWeatherReading(condition, temperature, high, low);
    }

    /// <summary>
    /// The next <paramref name="maxHours"/> hourly forecast points from the auto-discovered weather
    /// entity. Empty if there's no weather entity, or it has no hourly forecast.
    /// </summary>
    public static async Task<IReadOnlyList<HourlyForecastPoint>> GetHourlyAsync(string restBaseUrl, string token, int maxHours)
    {
        var baseUrl = restBaseUrl.TrimEnd('/');
        using var http = CreateClient(token);

        var entityId = await FindWeatherEntityAsync(http, baseUrl);
        if (entityId == null)
            return Array.Empty<HourlyForecastPoint>();

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
            .Take(maxHours)
            .ToArray();
    }

    /// <summary>
    /// Today's daily bucket. Best-effort: an entity with no daily forecast just yields no high/low,
    /// and the caller still prints the current conditions.
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
