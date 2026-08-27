namespace ReceiptPrinter.Configuration;

public enum BriefingLanguage { Nl, En }

/// <summary>
/// Runtime-configurable behaviour for the daily briefing and the Service's TODO-note checker - loaded
/// from briefing-settings.json (see <see cref="BriefingConfig.LoadSettings"/>), so it can be edited
/// without rebuilding/redeploying (e.g. on the Home Assistant add-on's persistent /data folder).
/// </summary>
public record BriefingSettings(
    BriefingLanguage Language = BriefingLanguage.Nl,
    List<string>? Widgets = null,
    bool TodoNotesEnabled = true,
    bool ScheduledBriefingEnabled = true,
    int ScheduledHour = 7,
    int ScheduledMinute = 0)
{
    /// <summary>Every widget, in the original briefing order - the default when Widgets is null/empty.</summary>
    public static readonly IReadOnlyList<string> DefaultWidgetOrder =
        ["DateHeader", "Weather", "Calendar", "Todo", "Energy"];
}
