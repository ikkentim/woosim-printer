using System.Net.Http.Headers;
using System.Text.Json;

namespace ReceiptPrinter;

/// <summary>
/// Reads a todo list stored in a Home Assistant entity, populated via a webhook-triggered template sensor.
/// </summary>
public static class HomeAssistantTodos
{
    /// <param name="attributeName">
    /// If set, reads the todo text from this attribute (unlimited length - use this with a template sensor's
    /// "items" attribute). If null, reads the entity's state instead (capped at 255 chars, e.g. input_text).
    /// </param>
    public static async Task<List<string>> GetAsync(string baseUrl, string token, string entityId, string? attributeName)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"{baseUrl.TrimEnd('/')}/api/states/{entityId}";
        var json = await http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);

        string text;
        if (string.IsNullOrEmpty(attributeName))
        {
            text = doc.RootElement.GetProperty("state").GetString() ?? "";
        }
        else
        {
            text = doc.RootElement.GetProperty("attributes").TryGetProperty(attributeName, out var attr)
                ? attr.GetString() ?? ""
                : "";
        }

        return text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.Length > 0 && l != "unknown" && l != "unavailable")
            .ToList();
    }
}
