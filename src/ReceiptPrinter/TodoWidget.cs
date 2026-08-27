namespace ReceiptPrinter;

public sealed class TodoWidget : IBriefingWidget
{
    public async Task RenderAsync(IReceiptPrinter printer)
    {
        var todos = await LoadAsync();

        printer.SetBold(true);
        printer.Line("TE DOEN");
        printer.SetBold(false);
        if (todos.Count == 0)
        {
            printer.Line("(niets op de lijst)");
        }
        else
        {
            foreach (var todo in todos)
                printer.Line($"- {todo}");
        }
        printer.Feed(1);
    }

    private static async Task<List<string>> LoadAsync()
    {
        var haConfig = BriefingConfig.LoadHa();
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

        var remindersConfig = BriefingConfig.LoadReminders();
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

        return BriefingConfig.LoadTodoFile();
    }
}
