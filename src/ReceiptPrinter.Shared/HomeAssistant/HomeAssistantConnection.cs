using ReceiptPrinter.Configuration;

namespace ReceiptPrinter.HomeAssistant;

/// <summary>
/// Resolves how to reach Home Assistant: an explicit BaseUrl+Token from config if both are set,
/// otherwise - when running as this repo's Home Assistant add-on, with `homeassistant_api: true` in
/// config.yaml - through Supervisor's proxy, authenticated with its automatically-injected
/// SUPERVISOR_TOKEN. No personal long-lived access token is needed in that case.
/// </summary>
public sealed record HomeAssistantConnection(string RestBaseUrl, string WebSocketUrl, string Token)
{
    public static HomeAssistantConnection? Resolve(HomeAssistantOptions options)
    {
        if (!string.IsNullOrEmpty(options.BaseUrl) && !string.IsNullOrEmpty(options.Token))
        {
            var baseUrl = options.BaseUrl.TrimEnd('/');
            var webSocketUrl = ToWebSocketScheme(baseUrl) + "/api/websocket";
            return new HomeAssistantConnection(baseUrl, webSocketUrl, options.Token);
        }

        var supervisorToken = Environment.GetEnvironmentVariable("SUPERVISOR_TOKEN");
        if (string.IsNullOrEmpty(supervisorToken))
            return null;

        // Supervisor's proxy uses different paths for REST vs WebSocket - unlike a direct HA URl,
        // WebSocket isn't under /core/api here.
        return new HomeAssistantConnection("http://supervisor/core", "ws://supervisor/core/websocket", supervisorToken);
    }

    private static string ToWebSocketScheme(string httpUrl) =>
        httpUrl.Replace("https://", "wss://").Replace("http://", "ws://");
}
