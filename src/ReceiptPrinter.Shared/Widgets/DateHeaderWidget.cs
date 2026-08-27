using ReceiptPrinter.Configuration;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Widgets;

public sealed class DateHeaderWidget : IBriefingWidget
{
    public Task<IReadOnlyList<IElement>> RenderAsync()
    {
        var culture = Localization.Culture;

        IReadOnlyList<IElement> elements =
        [
            new TextElement(DateTime.Now.ToString("dddd", culture), Bold: true, Width: 2, Height: 2, Justification: Justification.Center),
            new TextElement(DateTime.Now.ToString("d MMMM yyyy", culture), Justification: Justification.Center),
            new TextElement(""),
        ];

        return Task.FromResult(elements);
    }
}
