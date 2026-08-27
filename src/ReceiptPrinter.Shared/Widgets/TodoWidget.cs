using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Reminders;

namespace ReceiptPrinter.Widgets;

public sealed class TodoWidget : IBriefingWidget
{
    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var todos = await LoadAsync();
        var elements = new List<IElement> { new TextElement(Localization.T("todo.heading"), Bold: true) };

        if (todos.Count == 0)
        {
            elements.Add(new TextElement(Localization.T("todo.empty")));
        }
        else
        {
            foreach (var todo in todos)
                elements.Add(new TextElement($"- {todo}"));
        }
        elements.Add(new TextElement(""));

        return elements;
    }

    /// <summary>Loads the current to-do items - exposed for the Service's TODO-note diffing (see docs).</summary>
    public static async Task<List<string>> LoadAsync()
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
