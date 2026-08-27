namespace ReceiptPrinter.Configuration;

/// <summary>
/// The whole app's configuration, bound in one shot from IConfiguration (see
/// <see cref="ReceiptPrinterConfiguration"/>) - each nested class corresponds to a top-level config
/// section of the same name, so appsettings.json, an HA add-on's /data/options.json, and environment
/// variables (HomeAssistant__Token, etc.) all bind onto it the same way.
/// </summary>
public sealed class ReceiptPrinterOptions
{
    public LocationOptions Location { get; set; } = new();
    public HomeAssistantOptions HomeAssistant { get; set; } = new();
    public BriefingOptions Briefing { get; set; } = new();
}

public sealed class LocationOptions
{
    // Defaults to Kampen, Overijssel - override with your own coordinates for the weather widget.
    public double Latitude { get; set; } = 52.5546;
    public double Longitude { get; set; } = 5.9114;
    public string LocationName { get; set; } = "Kampen";
}

/// <summary>
/// BaseUrl/Token are only needed when NOT running as this repo's Home Assistant add-on - the add-on
/// instead reaches Home Assistant through Supervisor's proxy using its own automatically-injected
/// token, with no personal long-lived access token required. See <see cref="HomeAssistantConnection"/>.
/// </summary>
public sealed class HomeAssistantOptions
{
    public string? BaseUrl { get; set; }
    public string? Token { get; set; }

    public string TodoEntityId { get; set; } = "sensor.todo_list";
    public string? TodoAttributeName { get; set; } = "items";

    // Entity IDs feeding your Energy dashboard - leave unset to skip that line in the energy widget.
    // Grid import/export accept multiple entities so a two-tariff meter's totals get summed.
    public string? SolarProductionEntityId { get; set; }
    public string[]? GridImportEntityIds { get; set; }
    public string[]? GridExportEntityIds { get; set; }
    public string? GasEntityId { get; set; }
}

public enum BriefingLanguage { Nl, En }

public sealed class BriefingOptions
{
    public static readonly IReadOnlyList<string> DefaultWidgetOrder =
        ["DateHeader", "Weather", "Calendar", "Todo", "Energy"];

    public BriefingLanguage Language { get; set; } = BriefingLanguage.Nl;

    // Which widgets to run and in what order - null/empty falls back to DefaultWidgetOrder.
    public List<string>? Widgets { get; set; }

    public bool TodoNotesEnabled { get; set; } = true;
    public bool ScheduledBriefingEnabled { get; set; } = true;
    public int ScheduledHour { get; set; } = 7;
    public int ScheduledMinute { get; set; } = 0;
}
