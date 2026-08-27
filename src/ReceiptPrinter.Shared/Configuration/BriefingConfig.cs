using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReceiptPrinter.Configuration;

public record LocationConfig(double Latitude, double Longitude, string LocationName);

public record RemindersConfig(string AppleId, string AppSpecificPassword, string ListName);

public record HaConfig(string BaseUrl, string Token, string EntityId, string? AttributeName = null,
    string? SolarProductionEntityId = null, string[]? GridImportEntityIds = null, string[]? GridExportEntityIds = null,
    string? GasEntityId = null);

/// <summary>
/// Loads the local config files widgets need, auto-generating templates on first run.
/// </summary>
public static class BriefingConfig
{
    private static readonly string LocationConfigPath = ConfigPaths.Combine("briefing-config.json");
    private static readonly string HaConfigPath = ConfigPaths.Combine("ha-config.json");
    private static readonly string RemindersConfigPath = ConfigPaths.Combine("reminders-config.json");
    private static readonly string TodoPath = ConfigPaths.Combine("todo.txt");
    private static readonly string SettingsPath = ConfigPaths.Combine("briefing-settings.json");

    private static readonly JsonSerializerOptions SettingsJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }, // "nl"/"en", not "Nl"/"En"
    };

    public static LocationConfig LoadLocation()
    {
        if (File.Exists(LocationConfigPath))
        {
            var config = JsonSerializer.Deserialize<LocationConfig>(File.ReadAllText(LocationConfigPath));
            if (config != null)
                return config;
        }

        // Defaults to Kampen, Overijssel - edit briefing-config.json with your own coordinates.
        var defaultConfig = new LocationConfig(52.5546, 5.9114, "Kampen");
        File.WriteAllText(LocationConfigPath, JsonSerializer.Serialize(defaultConfig,
            new JsonSerializerOptions { WriteIndented = true }));
        return defaultConfig;
    }

    public static HaConfig? LoadHa()
    {
        if (File.Exists(HaConfigPath))
            return JsonSerializer.Deserialize<HaConfig>(File.ReadAllText(HaConfigPath));

        // Create a template - fill this in to pull todos/calendar/energy from Home Assistant.
        // SolarProductionEntityId/GridImportEntityIds/GridExportEntityIds/GasEntityId are the entity IDs
        // feeding your Energy dashboard (sum multiple entities, e.g. for a two-tariff meter) - leave
        // null/empty to skip that line in the energy widget.
        var template = new HaConfig("http://homeassistant.local:8123", "long-lived-access-token", "sensor.todo_list", "items",
            SolarProductionEntityId: null, GridImportEntityIds: null, GridExportEntityIds: null, GasEntityId: null);
        File.WriteAllText(HaConfigPath, JsonSerializer.Serialize(template,
            new JsonSerializerOptions { WriteIndented = true }));
        return null;
    }

    public static RemindersConfig? LoadReminders()
    {
        if (File.Exists(RemindersConfigPath))
            return JsonSerializer.Deserialize<RemindersConfig>(File.ReadAllText(RemindersConfigPath));

        // Create a template - fill this in and the todo widget will pull from Apple Reminders instead of todo.txt.
        var template = new RemindersConfig("you@icloud.com", "app-specific-password", "Reminders");
        File.WriteAllText(RemindersConfigPath, JsonSerializer.Serialize(template,
            new JsonSerializerOptions { WriteIndented = true }));
        return null;
    }

    public static BriefingSettings LoadSettings()
    {
        if (File.Exists(SettingsPath))
        {
            var settings = JsonSerializer.Deserialize<BriefingSettings>(File.ReadAllText(SettingsPath), SettingsJsonOptions);
            if (settings != null)
                return settings;
        }

        // Defaults match the original hardcoded behaviour: Dutch, every widget in the original order,
        // TODO notes and the scheduled daily briefing both enabled, printing at 07:00.
        var defaultSettings = new BriefingSettings(
            BriefingLanguage.Nl,
            BriefingSettings.DefaultWidgetOrder.ToList(),
            TodoNotesEnabled: true,
            ScheduledBriefingEnabled: true,
            ScheduledHour: 7,
            ScheduledMinute: 0);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(defaultSettings, SettingsJsonOptions));
        return defaultSettings;
    }

    public static List<string> LoadTodoFile()
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
}
