using System.Text.Json;

namespace ReceiptPrinter;

public static class DailyBriefing
{
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "briefing-config.json");
    private static readonly string TodoPath = Path.Combine(AppContext.BaseDirectory, "todo.txt");
    private static readonly string RemindersConfigPath = Path.Combine(AppContext.BaseDirectory, "reminders-config.json");
    private static readonly string HaConfigPath = Path.Combine(AppContext.BaseDirectory, "ha-config.json");

    private record Config(double Latitude, double Longitude, string LocationName);
    private record RemindersConfig(string AppleId, string AppSpecificPassword, string ListName);
    private record HaConfig(string BaseUrl, string Token, string EntityId, string? AttributeName = null);

    public static async Task PrintAsync(WoosimPrinter printer)
    {
        var config = LoadConfig();
        var weather = await GetWeatherAsync(config);
        var todos = await LoadTodosAsync();
        var (today, upcoming) = await LoadCalendarAsync();

        printer.SetJustification(WoosimPrinter.Justification.Center);
        printer.SetTextSize(2, 2);
        printer.SetBold(true);
        printer.Line(DateTime.Now.ToString("dddd"));
        printer.SetBold(false);
        printer.SetTextSize(1, 1);
        printer.Line(DateTime.Now.ToString("MMMM d, yyyy"));
        printer.Feed(1);

        printer.SetJustification(WoosimPrinter.Justification.Left);
        printer.Line(new string('-', 32));
        printer.Line(weather ?? "Weather unavailable");
        printer.Line(new string('-', 32));
        printer.Feed(1);

        printer.SetBold(true);
        printer.Line("TODAY");
        printer.SetBold(false);
        if (today.Count == 0)
        {
            printer.Line("(no events today)");
        }
        else
        {
            foreach (var ev in today)
                printer.Line($"- {(ev.AllDay ? "All day" : ev.Start.ToString("HH:mm"))}  {ev.Summary}");
        }
        printer.Feed(1);

        printer.SetBold(true);
        printer.Line("UPCOMING (next 14 days)");
        printer.SetBold(false);
        if (upcoming.Count == 0)
        {
            printer.Line("(nothing upcoming)");
        }
        else
        {
            foreach (var ev in upcoming.Take(3))
                printer.Line($"- {ev.Start:ddd d MMM}  {ev.Summary}");
        }
        printer.Feed(1);

        printer.SetBold(true);
        printer.Line("TO DO");
        printer.SetBold(false);
        if (todos.Count == 0)
        {
            printer.Line("(nothing on the list)");
        }
        else
        {
            foreach (var todo in todos)
                printer.Line($"- {todo}");
        }

        printer.Feed(3);
        printer.CutPaper();
    }

    private static Config LoadConfig()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<Config>(json);
            if (config != null)
                return config;
        }

        // Defaults to Kampen, Overijssel - edit briefing-config.json with your own coordinates.
        var defaultConfig = new Config(52.5546, 5.9114, "Kampen");
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(defaultConfig,
            new JsonSerializerOptions { WriteIndented = true }));
        return defaultConfig;
    }

    private static HaConfig? LoadHaConfig()
    {
        if (File.Exists(HaConfigPath))
            return JsonSerializer.Deserialize<HaConfig>(File.ReadAllText(HaConfigPath));

        // Create a template - fill this in to pull todos/calendar from Home Assistant instead of the other fallbacks.
        var template = new HaConfig("http://homeassistant.local:8123", "long-lived-access-token", "sensor.todo_list", "items");
        File.WriteAllText(HaConfigPath, JsonSerializer.Serialize(template,
            new JsonSerializerOptions { WriteIndented = true }));
        return null;
    }

    private static async Task<(List<CalendarEventInfo> Today, List<CalendarEventInfo> Upcoming)> LoadCalendarAsync()
    {
        var haConfig = LoadHaConfig();
        if (haConfig == null)
            return (new List<CalendarEventInfo>(), new List<CalendarEventInfo>());

        try
        {
            var start = DateTime.Today;
            var end = start.AddDays(14);
            var events = await HomeAssistantCalendar.GetEventsAsync(haConfig.BaseUrl, haConfig.Token, start, end);

            var today = events.Where(e => e.Start.Date == start).ToList();
            var upcoming = events.Where(e => e.Start.Date > start).ToList();
            return (today, upcoming);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Home Assistant calendar fetch failed: {ex}");
            return (new List<CalendarEventInfo>(), new List<CalendarEventInfo>());
        }
    }

    private static async Task<List<string>> LoadTodosAsync()
    {
        var haConfig = LoadHaConfig();
        if (haConfig != null)
        {
            try
            {
                return await HomeAssistantTodos.GetAsync(haConfig.BaseUrl, haConfig.Token, haConfig.EntityId, haConfig.AttributeName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Home Assistant todo fetch failed, falling back: {ex}");
            }
        }

        if (File.Exists(RemindersConfigPath))
        {
            var remindersConfig = JsonSerializer.Deserialize<RemindersConfig>(File.ReadAllText(RemindersConfigPath));
            if (remindersConfig != null)
            {
                try
                {
                    return await AppleReminders.GetIncompleteAsync(
                        remindersConfig.AppleId, remindersConfig.AppSpecificPassword, remindersConfig.ListName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Apple Reminders fetch failed, falling back to todo.txt: {ex}");
                }
            }
        }
        else
        {
            // Create a template - fill this in and the briefing will pull from Apple Reminders instead of todo.txt.
            var template = new RemindersConfig("you@icloud.com", "app-specific-password", "Reminders");
            File.WriteAllText(RemindersConfigPath, JsonSerializer.Serialize(template,
                new JsonSerializerOptions { WriteIndented = true }));
        }

        return LoadTodosFromFile();
    }

    private static List<string> LoadTodosFromFile()
    {
        if (!File.Exists(TodoPath))
        {
            File.WriteAllText(TodoPath, "");
            return new List<string>();
        }

        return File.ReadAllLines(TodoPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    private static async Task<string?> GetWeatherAsync(Config config)
    {
        try
        {
            using var http = new HttpClient();
            var lat = config.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lon = config.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                      "&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min" +
                      "&timezone=auto";
            var json = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var current = doc.RootElement.GetProperty("current");
            var temp = current.GetProperty("temperature_2m").GetDouble();
            var code = current.GetProperty("weather_code").GetInt32();

            var daily = doc.RootElement.GetProperty("daily");
            var tMax = daily.GetProperty("temperature_2m_max")[0].GetDouble();
            var tMin = daily.GetProperty("temperature_2m_min")[0].GetDouble();

            return $"{config.LocationName}: {DescribeWeather(code)}, {temp:0.#}C now\n" +
                   $"H:{tMax:0.#}C L:{tMin:0.#}C";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Weather fetch failed: {ex}");
            return null;
        }
    }

    private static string DescribeWeather(int code) => code switch
    {
        0 => "Clear sky",
        1 or 2 or 3 => "Partly cloudy",
        45 or 48 => "Fog",
        51 or 53 or 55 => "Drizzle",
        61 or 63 or 65 => "Rain",
        71 or 73 or 75 => "Snow",
        80 or 81 or 82 => "Rain showers",
        95 or 96 or 99 => "Thunderstorm",
        _ => "Unknown",
    };
}
