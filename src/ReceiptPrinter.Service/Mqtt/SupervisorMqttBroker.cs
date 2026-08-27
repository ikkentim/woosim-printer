using System.Net.Http.Headers;
using System.Text.Json;

namespace ReceiptPrinter.Service.Mqtt;

/// <summary>
/// Broker connection details for whatever MQTT integration/add-on is configured in Home Assistant
/// (e.g. the official Mosquitto broker add-on), obtained via Supervisor's Services API rather than
/// asked of the user - this only works when the add-on declares `services: [mqtt:want]` in config.yaml
/// and homeassistant_api-style Supervisor access (SUPERVISOR_TOKEN) is available.
/// </summary>
public sealed record SupervisorMqttBroker(string Host, int Port, bool Ssl, string? Username, string? Password)
{
    /// <summary>Returns null if there's no MQTT service registered with Supervisor (e.g. no broker
    /// add-on installed) - that's a normal, expected outcome, not an error.</summary>
    public static async Task<SupervisorMqttBroker?> ResolveAsync(string supervisorToken, CancellationToken ct)
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://supervisor/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", supervisorToken);

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync("services/mqtt", ct);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var root = doc.RootElement;
        if (!root.TryGetProperty("result", out var result) || result.GetString() != "ok")
            return null;

        if (!root.TryGetProperty("data", out var data))
            return null;

        return new SupervisorMqttBroker(
            data.GetProperty("host").GetString()!,
            data.GetProperty("port").GetInt32(),
            data.TryGetProperty("ssl", out var ssl) && ssl.ValueKind == JsonValueKind.True,
            data.TryGetProperty("username", out var u) ? u.GetString() : null,
            data.TryGetProperty("password", out var p) ? p.GetString() : null);
    }
}
