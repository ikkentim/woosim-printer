namespace ReceiptPrinter.Service;

/// <summary>
/// Checks the current to-do list against what's already been printed. Anything new gets its own
/// little "TODO" note printed; anything that's disappeared from the source is just forgotten (it was
/// presumably completed and physically thrown away, per the fridge-note workflow this is built for).
/// </summary>
public sealed class TodoNoteChecker
{
    private readonly TodoNoteStore _store;

    public TodoNoteChecker(TodoNoteStore store)
    {
        _store = store;
    }

    public async Task CheckAndPrintAsync(IReceiptPrinter printer)
    {
        var current = new HashSet<string>(await TodoWidget.LoadAsync());
        var alreadyPrinted = _store.Load();

        var newItems = current.Except(alreadyPrinted).ToList();

        foreach (var item in newItems)
            await printer.PrintAsync(BuildNote(item));

        // Whatever's no longer in the source (finished, deleted) just drops out here - nothing to print,
        // it simply stops being tracked.
        _store.Save(current);
    }

    private static Receipt BuildNote(string item)
    {
        IReadOnlyList<IElement> elements =
        [
            new TextElement("TODO", Bold: true, Width: 2, Height: 2, Justification: Justification.Center),
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
