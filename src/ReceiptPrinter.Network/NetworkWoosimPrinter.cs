using ReceiptPrinter.Printers;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Printers.Network;

/// <summary>
/// Drives a Woosim printer over the network: encodes the <see cref="Receipt"/> to a raw ESC/POS byte
/// stream (<see cref="EscPosEncoder"/>) here on the sender, then POSTs those bytes to a small HTTP
/// service sitting next to the actual printer, which copies them straight to the serial port. Today
/// that service is ReceiptPrinter.NetworkSerialService, standing in for the ESP32 firmware planned in
/// docs/HARDWARE.md; the wire protocol is deliberately trivial (POST /print, an
/// <c>application/octet-stream</c> body of ESC/POS bytes) so the firmware only has to pipe HTTP to
/// UART.
/// </summary>
public sealed class NetworkWoosimPrinter : IReceiptPrinter
{
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
        using var content = new ByteArrayContent(EscPosEncoder.Encode(receipt));
        content.Headers.ContentType = new("application/octet-stream");

        using var response = await _http.PostAsync("print", content);
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
