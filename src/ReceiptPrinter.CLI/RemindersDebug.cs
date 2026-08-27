using System.Text.Json;

namespace ReceiptPrinter;

public static class RemindersDebug
{
    public static async Task RunAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "reminders-config.json");
        if (!File.Exists(path))
        {
            Console.WriteLine("reminders-config.json not found - run the 'briefing' command once first to generate it.");
            return;
        }

        var config = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(path));
        var appleId = config.GetProperty("AppleId").GetString()!;
        var password = config.GetProperty("AppSpecificPassword").GetString()!;

        await AppleReminders.DebugDumpAllAsync(appleId, password);
    }
}
