using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

public sealed class TodoWidget(HomeAssistantOptions homeAssistant) : IBriefingWidget
{
    public async Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var todos = await LoadAsync(homeAssistant);
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
    public static async Task<List<string>> LoadAsync(HomeAssistantOptions homeAssistant)
    {
        var connection = HomeAssistantConnection.Resolve(homeAssistant);
        if (connection != null)
        {
            try
            {
                return await HomeAssistantTodos.GetAsync(connection.RestBaseUrl, connection.Token,
                    homeAssistant.TodoEntityId, homeAssistant.TodoAttributeName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Home Assistant todo fetch failed, falling back to todo.txt: {ex}");
            }
        }

        return TodoFile.Load();
    }
}
