using System.Globalization;
using System.Text.Json;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

public sealed class WeatherWidget : IBriefingWidget
{
    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var config = BriefingConfig.LoadLocation();
        var weather = await GetWeatherAsync(config);

        return
        [
            new TextElement(new string('-', 32)),
            new TextElement(weather ?? "Weer niet beschikbaar"),
            new TextElement(new string('-', 32)),
            new TextElement(""),
        ];
    }

    private static async Task<string?> GetWeatherAsync(LocationConfig config)
    {
        try
        {
            using var http = new HttpClient();
            var lat = config.Latitude.ToString(CultureInfo.InvariantCulture);
            var lon = config.Longitude.ToString(CultureInfo.InvariantCulture);
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

            return $"{config.LocationName}: {DescribeWeather(code)}, {temp:0.#}C nu\n" +
                   $"Max:{tMax:0.#}C Min:{tMin:0.#}C";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Weather fetch failed: {ex}");
            return null;
        }
    }

    private static string DescribeWeather(int code) => code switch
    {
        0 => "Helder",
        1 or 2 or 3 => "Half bewolkt",
        45 or 48 => "Mist",
        51 or 53 or 55 => "Motregen",
        61 or 63 or 65 => "Regen",
        71 or 73 or 75 => "Sneeuw",
        80 or 81 or 82 => "Regenbuien",
        95 or 96 or 99 => "Onweer",
        _ => "Onbekend",
    };
}
