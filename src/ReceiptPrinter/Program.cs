using System.Text;
using ReceiptPrinter;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var command = args.Length > 0 ? args[0] : "test";

if (command == "reminders-debug")
{
    await RemindersDebug.RunAsync();
    return;
}

var portName = args.Length > 1 ? args[1] : "COM3";
var baudRate = args.Length > 2 ? int.Parse(args[2]) : 9600;

Console.WriteLine($"Connecting to {portName} @ {baudRate} baud...");

using var printer = new WoosimPrinter(portName, baudRate);
printer.Open();
printer.Initialize();

switch (command)
{
    case "briefing":
        await DailyBriefing.PrintAsync(printer);
        break;
    case "test":
    default:
        RunTestPrint(printer);
        break;
}

Console.WriteLine("Done.");

static void RunTestPrint(WoosimPrinter printer)
{
    printer.SetJustification(WoosimPrinter.Justification.Center);
    printer.SetTextSize(2, 2);
    printer.SetBold(true);
    printer.Line("HELLO");
    printer.SetBold(false);
    printer.SetTextSize(1, 1);
    printer.Feed(1);

    printer.SetJustification(WoosimPrinter.Justification.Left);
    printer.Line($"Printer test - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    printer.Line(new string('-', 32));
    printer.SetUnderline(true);
    printer.Line("Underlined line");
    printer.SetUnderline(false);
    printer.Line("Plain line");

    printer.Feed(3);
    printer.CutPaper();
}
