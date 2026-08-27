using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ReceiptPrinter.HomeAssistant;

public record EnergySummary(double? ProducedKwh, double? GridImportKwh, double? GridExportKwh, double? GasM3);

/// <summary>
/// Reads yesterday's totals for Energy-dashboard sensors directly from Home Assistant's long-term
/// statistics, via the WebSocket API (there's no REST equivalent for this data).
/// </summary>
public static class HomeAssistantEnergy
{
    public static async Task<EnergySummary> GetYesterdayAsync(string baseUrl, string token,
        string? productionEntityId, IReadOnlyList<string>? gridImportEntityIds, IReadOnlyList<string>? gridExportEntityIds,
        string? gasEntityId)
    {
        gridImportEntityIds ??= Array.Empty<string>();
        gridExportEntityIds ??= Array.Empty<string>();

        var statisticIds = (new[] { productionEntityId, gasEntityId })
            .Concat(gridImportEntityIds)
            .Concat(gridExportEntityIds)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct()
            .ToArray();

        if (statisticIds.Length == 0)
            return new EnergySummary(null, null, null, null);

        var changes = await GetYesterdayChangesAsync(baseUrl, token, statisticIds);

        return new EnergySummary(
            productionEntityId != null && changes.TryGetValue(productionEntityId, out var p) ? p : null,
            Sum(changes, gridImportEntityIds),
            Sum(changes, gridExportEntityIds),
            gasEntityId != null && changes.TryGetValue(gasEntityId, out var g) ? g : null);
    }

    private static double? Sum(Dictionary<string, double> changes, IReadOnlyList<string> entityIds)
    {
        var values = entityIds.Where(changes.ContainsKey).Select(id => changes[id]).ToList();
        return values.Count > 0 ? values.Sum() : null;
    }

    private static async Task<Dictionary<string, double>> GetYesterdayChangesAsync(string baseUrl, string token, string[] statisticIds)
    {
        var wsUrl = baseUrl.TrimEnd('/').Replace("https://", "wss://").Replace("http://", "ws://") + "/api/websocket";

        using var ws = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await ws.ConnectAsync(new Uri(wsUrl), cts.Token);

        await ReceiveJsonAsync(ws, cts.Token); // auth_required

        await SendJsonAsync(ws, new { type = "auth", access_token = token }, cts.Token);
        var authResult = await ReceiveJsonAsync(ws, cts.Token);
        if (authResult.RootElement.GetProperty("type").GetString() != "auth_ok")
            throw new InvalidOperationException("Home Assistant WebSocket authentication failed.");

        var yesterday = DateTime.Today.AddDays(-1);

        // Pad the requested window well beyond just "yesterday" - HA buckets "day" periods to its own
        // configured timezone's midnight, which won't necessarily line up with a UTC-converted exact
        // one-day window (e.g. if this machine and HA disagree on local timezone). Instead of trusting
        // the window to yield exactly one clean bucket, request a few days and pick out the one bucket
        // that actually matches yesterday's calendar date below.
        var queryStart = DateTime.Today.AddDays(-3);
        var queryEnd = DateTime.Today.AddDays(1);

        await SendJsonAsync(ws, new
        {
            id = 1,
            type = "recorder/statistics_during_period",
            start_time = queryStart.ToUniversalTime().ToString("o"),
            end_time = queryEnd.ToUniversalTime().ToString("o"),
            statistic_ids = statisticIds,
            period = "day",
            types = new[] { "change" },
        }, cts.Token);

        var response = await ReceiveJsonAsync(ws, cts.Token);
        var result = new Dictionary<string, double>();

        if (!response.RootElement.GetProperty("success").GetBoolean())
        {
            var error = response.RootElement.TryGetProperty("error", out var err) ? err.ToString() : "unknown error";
            throw new InvalidOperationException($"Statistics query failed: {error}");
        }

        foreach (var stat in response.RootElement.GetProperty("result").EnumerateObject())
        {
            var match = stat.Value.EnumerateArray()
                .FirstOrDefault(p => p.TryGetProperty("start", out var s) && ParsePeriodStart(s).Date == yesterday.Date);

            if (match.ValueKind == JsonValueKind.Object && match.TryGetProperty("change", out var change) && change.ValueKind == JsonValueKind.Number)
                result[stat.Name] = change.GetDouble();
        }

        try
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token);
        }
        catch
        {
            // best-effort close
        }

        return result;
    }

    private static DateTime ParsePeriodStart(JsonElement start) => start.ValueKind switch
    {
        JsonValueKind.Number => DateTimeOffset.FromUnixTimeMilliseconds((long)start.GetDouble()).LocalDateTime,
        JsonValueKind.String => DateTimeOffset.Parse(start.GetString()!).LocalDateTime,
        _ => DateTime.MinValue,
    };

    private static async Task SendJsonAsync(ClientWebSocket ws, object payload, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static async Task<JsonDocument> ReceiveJsonAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();

        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, ct);
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        stream.Position = 0;
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}
