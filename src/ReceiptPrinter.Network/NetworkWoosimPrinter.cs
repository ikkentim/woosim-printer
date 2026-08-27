using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Printers.Network;

/// <summary>
/// Drives a Woosim printer over the network by POSTing the Receipt as JSON to a small HTTP service
/// sitting next to the actual printer. Today that's ReceiptPrinter.NetworkSerialService, standing in
/// for the ESP32 firmware planned in docs/HARDWARE.md; once that firmware exists it just needs to speak
/// the same tiny wire protocol (POST /print, JSON body) for this class to keep working unchanged.
/// </summary>
public sealed class NetworkWoosimPrinter : IReceiptPrinter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    /// <param name="host">Host (optionally "host:port") or full base URL of the printer service, e.g.
    /// "192.168.1.50:5251" or "http://printer-pc.local:5251".</param>
    public NetworkWoosimPrinter(string host)
    {
        var baseUrl = host.Contains("://") ? host : $"http://{host}";
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15),
        };
    }

    public async Task PrintAsync(Receipt receipt)
    {
        var response = await _http.PostAsJsonAsync("print", receipt, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Hits the network printer service's /health endpoint - never touches the printer itself.</summary>
    public async Task<bool> PingAsync()
    {
        try
        {
            using var response = await _http.GetAsync("health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose() => _http.Dispose();
}
