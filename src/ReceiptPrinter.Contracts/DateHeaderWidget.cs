using System.Globalization;

namespace ReceiptPrinter;

public sealed class DateHeaderWidget : IBriefingWidget
{
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

    public Task<IReadOnlyList<IElement>> RenderAsync()
    {
        IReadOnlyList<IElement> elements =
        [
            new TextElement(DateTime.Now.ToString("dddd", Dutch), Bold: true, Width: 2, Height: 2, Justification: Justification.Center),
            new TextElement(DateTime.Now.ToString("d MMMM yyyy", Dutch), Justification: Justification.Center),
            new TextElement(""),
        ];

        return Task.FromResult(elements);
    }
}
