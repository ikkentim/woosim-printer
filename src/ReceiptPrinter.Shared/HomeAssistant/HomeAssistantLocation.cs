using System.Text.Json;

namespace ReceiptPrinter.HomeAssistant;

/// <summary>
/// Home Assistant already knows its own location (Settings -> System -> General) - rather than asking
/// the user to enter latitude/longitude a second time for the weather widget, this reads it straight
/// from Home Assistant's own `/api/config` endpoint.
/// </summary>
public static class HomeAssistantLocation
{
    public static async Task<(double Latitude, double Longitude)?> GetAsync(string restBaseUrl, string token)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var json = await http.GetStringAsync($"{restBaseUrl.TrimEnd('/')}/api/config");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("latitude", out var lat) || !root.TryGetProperty("longitude", out var lon))
            return null;

        return (lat.GetDouble(), lon.GetDouble());
    }
}
