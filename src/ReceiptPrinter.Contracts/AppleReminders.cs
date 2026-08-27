using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Ical.Net;
using Ical.Net.CalendarComponents;

namespace ReceiptPrinter;

/// <summary>
/// Reads incomplete items from an iCloud Reminders list via CalDAV.
/// </summary>
public static class AppleReminders
{
    private static readonly XNamespace D = "DAV:";
    private static readonly XNamespace C = "urn:ietf:params:xml:ns:caldav";

    public static async Task<List<string>> GetIncompleteAsync(string appleId, string appSpecificPassword, string listName)
    {
        using var http = CreateHttp();
        var auth = BasicAuth(appleId, appSpecificPassword);

        var homeSetUrl = await DiscoverHomeSetAsync(http, auth);
        var lists = await ListRemindersCollectionsAsync(http, auth, homeSetUrl);

        var match = lists.FirstOrDefault(l => string.Equals(l.Name, listName, StringComparison.OrdinalIgnoreCase));
        if (match.Href == null)
            throw new InvalidOperationException($"No reminders list named '{listName}' found.");

        var todos = await FetchTodosAsync(http, auth, match.Href);
        return todos
            .Where(t => t.Status is not ("COMPLETED" or "CANCELLED") && !string.IsNullOrWhiteSpace(t.Summary))
            .Select(t => t.Summary!)
            .ToList();
    }

    /// <summary>Lists every reminders collection along with every item in it (any status) - for debugging.</summary>
    public static async Task DebugDumpAllAsync(string appleId, string appSpecificPassword)
    {
        using var http = CreateHttp();
        var auth = BasicAuth(appleId, appSpecificPassword);

        var homeSetUrl = await DiscoverHomeSetAsync(http, auth);
        var lists = await ListRemindersCollectionsAsync(http, auth, homeSetUrl);

        foreach (var (name, href) in lists)
        {
            Console.WriteLine($"\n=== List: '{name}' ({href}) ===");
            List<(string? Status, string? Summary)> todos;
            try
            {
                todos = await FetchTodosAsync(http, auth, href);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  (failed to fetch: {ex})");
                continue;
            }

            if (todos.Count == 0)
            {
                Console.WriteLine("  (empty)");
                continue;
            }

            foreach (var t in todos)
                Console.WriteLine($"  [{t.Status ?? "NEEDS-ACTION"}] {t.Summary}");
        }
    }

    private static HttpClient CreateHttp() => new(new HttpClientHandler { AllowAutoRedirect = false });

    private static AuthenticationHeaderValue BasicAuth(string appleId, string appSpecificPassword) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{appleId}:{appSpecificPassword}")));

    private static async Task<string> DiscoverHomeSetAsync(HttpClient http, AuthenticationHeaderValue auth)
    {
        var principalUrl = await DiscoverAsync(http, auth, "https://caldav.icloud.com/",
            "<d:propfind xmlns:d=\"DAV:\"><d:prop><d:current-user-principal/></d:prop></d:propfind>",
            "current-user-principal");

        return await DiscoverAsync(http, auth, principalUrl,
            "<d:propfind xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\">" +
            "<d:prop><c:calendar-home-set/></d:prop></d:propfind>",
            "calendar-home-set");
    }

    private static async Task<string> DiscoverAsync(HttpClient http, AuthenticationHeaderValue auth, string url, string body, string propName)
    {
        for (var redirects = 0; redirects < 5; redirects++)
        {
            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml"),
            };
            request.Headers.Authorization = auth;
            request.Headers.Add("Depth", "0");

            var response = await http.SendAsync(request);

            if (response.StatusCode is System.Net.HttpStatusCode.MovedPermanently
                or System.Net.HttpStatusCode.Found
                or System.Net.HttpStatusCode.TemporaryRedirect)
            {
                url = response.Headers.Location!.IsAbsoluteUri
                    ? response.Headers.Location!.ToString()
                    : new Uri(new Uri(url), response.Headers.Location!).ToString();
                continue;
            }

            response.EnsureSuccessStatusCode();
            var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
            var href = xml.Descendants(D + "href").FirstOrDefault(h => IsWithin(h, propName));
            if (href == null)
                throw new InvalidOperationException($"CalDAV discovery failed: '{propName}' not found in response.");

            return href.Value.StartsWith("http") ? href.Value : $"https://{new Uri(url).Host}{href.Value}";
        }

        throw new InvalidOperationException("Too many redirects during CalDAV discovery.");
    }

    private static bool IsWithin(XElement href, string ancestorLocalName) =>
        href.Ancestors().Any(a => a.Name.LocalName == ancestorLocalName);

    private static async Task<List<(string Name, string Href)>> ListRemindersCollectionsAsync(HttpClient http, AuthenticationHeaderValue auth, string homeSetUrl)
    {
        var results = new List<(string, string)>();
        await CollectRemindersCollectionsAsync(http, auth, homeSetUrl, results, depth: 0);
        return results;
    }

    private static async Task CollectRemindersCollectionsAsync(HttpClient http, AuthenticationHeaderValue auth, string collectionUrl,
        List<(string Name, string Href)> results, int depth)
    {
        if (depth > 3)
            return; // safety limit, Reminders folders are only ever one level deep in practice

        var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), collectionUrl)
        {
            Content = new StringContent(
                "<d:propfind xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\">" +
                "<d:prop><d:displayname/><d:resourcetype/><c:supported-calendar-component-set/></d:prop>" +
                "</d:propfind>", Encoding.UTF8, "application/xml"),
        };
        request.Headers.Authorization = auth;
        request.Headers.Add("Depth", "1");

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());

        var basePath = new Uri(collectionUrl).AbsolutePath.TrimEnd('/');
        var host = new Uri(collectionUrl).Host;

        foreach (var responseEl in xml.Descendants(D + "response"))
        {
            var href = responseEl.Element(D + "href")!.Value;
            if (href.TrimEnd('/') == basePath || href.TrimEnd('/').EndsWith("/outbox"))
                continue; // skip the collection itself and the scheduling outbox, both returned by Depth:1

            var isCollection = responseEl.Descendants(D + "resourcetype").Descendants(D + "collection").Any();
            if (!isCollection)
                continue;

            var supportsVTodo = responseEl.Descendants(C + "comp")
                .Any(c => c.Attribute("name")?.Value == "VTODO");
            var displayName = responseEl.Descendants(D + "displayname").FirstOrDefault()?.Value ?? "";
            var childUrl = $"https://{host}{href}";

            if (supportsVTodo)
                results.Add((displayName, childUrl));
            else
                // Not a reminders list itself - likely a folder grouping other lists. Recurse into it.
                await CollectRemindersCollectionsAsync(http, auth, childUrl, results, depth + 1);
        }
    }

    private static async Task<List<(string? Status, string? Summary)>> FetchTodosAsync(HttpClient http, AuthenticationHeaderValue auth, string listUrl)
    {
        var request = new HttpRequestMessage(new HttpMethod("REPORT"), listUrl)
        {
            Content = new StringContent(
                "<c:calendar-query xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:caldav\">" +
                "<d:prop><d:getetag/><c:calendar-data/></d:prop>" +
                "<c:filter><c:comp-filter name=\"VCALENDAR\"><c:comp-filter name=\"VTODO\"/></c:comp-filter></c:filter>" +
                "</c:calendar-query>", Encoding.UTF8, "application/xml"),
        };
        request.Headers.Authorization = auth;
        request.Headers.Add("Depth", "1");

        var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());

        var results = new List<(string?, string?)>();
        foreach (var data in xml.Descendants(C + "calendar-data"))
        {
            if (string.IsNullOrWhiteSpace(data.Value))
            {
                Console.WriteLine("  (empty calendar-data element, skipping)");
                continue;
            }

            Calendar? calendar;
            try
            {
                calendar = Calendar.Load(data.Value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  (failed to parse calendar-data: {ex.Message})");
                Console.WriteLine($"  Raw:\n{data.Value}");
                continue;
            }

            if (calendar == null)
            {
                Console.WriteLine("  (Calendar.Load returned null for this item)");
                Console.WriteLine($"  Raw:\n{data.Value}");
                continue;
            }

            foreach (var todo in calendar.Todos)
                results.Add((todo.Status, todo.Summary));
        }

        return results;
    }
}
