using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReceiptPrinter.HomeAssistant;

/// <summary>Current conditions plus today's high/low, as read from a Home Assistant weather entity.</summary>
public record HomeAssistantWeatherReading(string Condition, double? Temperature, double? TempHigh, double? TempLow);

/// <summary>
/// Reads current weather from Home Assistant's own weather integration rather than calling a
/// third-party API: auto-discovers the first <c>weather.*</c> entity, takes its state (the condition)
/// and <c>temperature</c> attribute, then asks the <c>weather.get_forecasts</c> service for today's
/// daily high/low (modern Home Assistant no longer exposes a <c>forecast</c> attribute directly).
/// </summary>
public static class HomeAssistantWeather
{
    public static async Task<HomeAssistantWeatherReading?> GetAsync(string restBaseUrl, string token)
    {
        var baseUrl = restBaseUrl.TrimEnd('/');
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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

    private static async Task<string?> FindWeatherEntityAsync(HttpClient http, string baseUrl)
    {
        var json = await http.GetStringAsync($"{baseUrl}/api/states");
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.EnumerateArray()
            .Select(e => e.TryGetProperty("entity_id", out var id) ? id.GetString() : null)
            .FirstOrDefault(id => id != null && id.StartsWith("weather.", StringComparison.Ordinal));
    }

    /// <summary>
    /// Today's forecast bucket via <c>weather.get_forecasts</c>. Best-effort: an entity that only
    /// supports hourly forecasts (or none) just yields no high/low, and the caller still prints the
    /// current conditions.
    /// </summary>
    private static async Task<(double? High, double? Low)> GetTodayHighLowAsync(HttpClient http, string baseUrl, string entityId)
    {
        try
        {
            using var body = new StringContent(
                JsonSerializer.Serialize(new { entity_id = entityId, type = "daily" }),
                Encoding.UTF8, "application/json");

            using var response = await http.PostAsync($"{baseUrl}/api/services/weather/get_forecasts?return_response", body);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var forecast = doc.RootElement.GetProperty("service_response").GetProperty(entityId).GetProperty("forecast");
            if (forecast.GetArrayLength() == 0)
                return (null, null);

            var today = forecast[0];
            return (GetNumber(today, "temperature"), GetNumber(today, "templow"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Home Assistant weather forecast fetch failed (current conditions still shown): {ex}");
            return (null, null);
        }
    }

    private static double? GetNumber(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
}
