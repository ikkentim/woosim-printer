using System.Globalization;

namespace ReceiptPrinter.Configuration;

/// <summary>
/// A small NL/EN string table for the briefing widgets, plus the CultureInfo to format dates/numbers
/// with. Something building a briefing (DailyBriefing.BuildAsync, the Service's TodoNoteChecker) calls
/// <see cref="SetLanguage"/> once per run, from briefing-settings.json, before rendering anything.
/// </summary>
public static class Localization
{
    private static readonly Dictionary<BriefingLanguage, CultureInfo> Cultures = new()
    {
        [BriefingLanguage.Nl] = CultureInfo.GetCultureInfo("nl-NL"),
        [BriefingLanguage.En] = CultureInfo.GetCultureInfo("en-US"),
    };

    private static readonly Dictionary<string, (string Nl, string En)> Strings = new()
    {
        ["weather.unavailable"] = ("Weer niet beschikbaar", "Weather unavailable"),
        ["weather.now"] = ("nu", "now"),
        ["weather.max"] = ("Max", "Max"),
        ["weather.min"] = ("Min", "Min"),
        ["weather.clear"] = ("Helder", "Clear"),
        ["weather.partly_cloudy"] = ("Half bewolkt", "Partly cloudy"),
        ["weather.cloudy"] = ("Bewolkt", "Cloudy"),
        ["weather.windy"] = ("Winderig", "Windy"),
        ["weather.rain_heading"] = ("Neerslag per uur (mm)", "Precipitation by hour (mm)"),
        ["weather.rain_dry"] = ("Vandaag droog", "Dry today"),
        ["weather.rain_total"] = ("Totaal {0} mm", "Total {0} mm"),
        ["weather.fog"] = ("Mist", "Fog"),
        ["weather.drizzle"] = ("Motregen", "Drizzle"),
        ["weather.rain"] = ("Regen", "Rain"),
        ["weather.snow"] = ("Sneeuw", "Snow"),
        ["weather.showers"] = ("Regenbuien", "Showers"),
        ["weather.thunder"] = ("Onweer", "Thunderstorm"),
        ["weather.unknown"] = ("Onbekend", "Unknown"),
        ["calendar.today"] = ("VANDAAG", "TODAY"),
        ["calendar.none_today"] = ("(geen afspraken vandaag)", "(no appointments today)"),
        ["calendar.upcoming"] = ("AANKOMEND (komende 14 dagen)", "UPCOMING (next 14 days)"),
        ["calendar.none_upcoming"] = ("(niets aankomend)", "(nothing upcoming)"),
        ["calendar.all_day"] = ("Hele dag", "All day"),
        ["todo.heading"] = ("TE DOEN", "TO DO"),
        ["todo.empty"] = ("(niets op de lijst)", "(nothing on the list)"),
        ["todo.note_heading"] = ("TODO", "TO DO"),
        ["energy.heading"] = ("ENERGIE (gisteren)", "ENERGY (yesterday)"),
        ["energy.produced"] = ("Geproduceerd", "Produced"),
        ["energy.grid_import"] = ("Van net", "From grid"),
        ["energy.grid_export"] = ("Teruggeleverd", "Exported"),
        ["energy.gas"] = ("Gas", "Gas"),
    };

    public static BriefingLanguage Current { get; private set; } = BriefingLanguage.Nl;

    public static CultureInfo Culture => Cultures[Current];

    public static void SetLanguage(BriefingLanguage language) => Current = language;

    /// <summary>Translates a string table key into the current language - returns the key itself if unknown.</summary>
    public static string T(string key)
    {
        if (!Strings.TryGetValue(key, out var pair))
            return key;

        return Current == BriefingLanguage.Nl ? pair.Nl : pair.En;
    }
}
