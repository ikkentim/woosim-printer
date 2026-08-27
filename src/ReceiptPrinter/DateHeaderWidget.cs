using System.Globalization;

namespace ReceiptPrinter;

public sealed class DateHeaderWidget : IBriefingWidget
{
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

    public Task RenderAsync(IReceiptPrinter printer)
    {
        printer.SetJustification(Justification.Center);
        printer.SetTextSize(2, 2);
        printer.SetBold(true);
        printer.Line(DateTime.Now.ToString("dddd", Dutch));
        printer.SetBold(false);
        printer.SetTextSize(1, 1);
        printer.Line(DateTime.Now.ToString("d MMMM yyyy", Dutch));
        printer.Feed(1);
        printer.SetJustification(Justification.Left);

        return Task.CompletedTask;
    }
}
