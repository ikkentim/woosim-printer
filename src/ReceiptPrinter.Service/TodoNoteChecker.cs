using Microsoft.Extensions.Options;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Widgets;

namespace ReceiptPrinter.Service;

/// <summary>
/// Checks the current to-do list against what's already been printed. Anything new gets its own
/// little "TODO" note printed; anything that's disappeared from the source is just forgotten (it was
/// presumably completed and physically thrown away, per the fridge-note workflow this is built for).
/// Controlled by ReceiptPrinterOptions.Briefing.TodoNotesEnabled.
/// </summary>
public sealed class TodoNoteChecker(TodoNoteStore store, IOptionsMonitor<ReceiptPrinterOptions> options)
{
    public async Task CheckAndPrintAsync(IReceiptPrinter printer)
    {
        var settings = options.CurrentValue;
        if (!settings.Briefing.TodoNotesEnabled)
            return;

        Localization.SetLanguage(settings.Briefing.Language);

        var current = new HashSet<string>(await TodoWidget.LoadAsync(settings.HomeAssistant));
        var alreadyPrinted = store.Load();

        var newItems = current.Except(alreadyPrinted).ToList();

        foreach (var item in newItems)
            await printer.PrintAsync(BuildNote(item));

        // Whatever's no longer in the source (finished, deleted) just drops out here - nothing to print,
        // it simply stops being tracked.
        store.Save(current);
    }

    private static Receipt BuildNote(string item)
    {
        IReadOnlyList<IElement> elements =
        [
            new TextElement(Localization.T("todo.note_heading"), Bold: true, Width: 2, Height: 2, Justification: Justification.Center),
            new TextElement(""),
            new TextElement(item, Justification: Justification.Center),
            new TextElement(DateTime.Now.ToString("dd-MM-yyyy"), Justification: Justification.Center),
            new TextElement(""),
            new TextElement(""),
            new TextElement(""),
            new TextElement(""),
            new TextElement(""),
            new TextElement(""),
        ];

        return new Receipt(elements, CutStyle.Partial);
    }
}
