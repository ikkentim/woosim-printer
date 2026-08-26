using System.Text;
using ReceiptPrinter;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Usage:
//   dotnet run -- <command> [printer-type] [printer-args...]
//   command:      test | briefing | reminders-debug
//   printer-type: serial (default) [port=COM3] [baud=9600]
//                 network           [host]      (TODO: not implemented yet)
var command = args.Length > 0 ? args[0] : "test";

if (command == "reminders-debug")
{
    await RemindersDebug.RunAsync();
    return;
}

using var printer = CreatePrinter(args.Skip(1).ToArray());
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

static IReceiptPrinter CreatePrinter(string[] printerArgs)
{
    var printerType = printerArgs.Length > 0 ? printerArgs[0] : "serial";

    switch (printerType)
    {
        case "network":
            var host = printerArgs.Length > 1 ? printerArgs[1] : "printer.local";
            Console.WriteLine($"Using network printer at {host} (TODO: not implemented yet)...");
            return new NetworkWoosimPrinter(host);

        case "serial":
        default:
            var portName = printerArgs.Length > 1 ? printerArgs[1] : "COM3";
            var baudRate = printerArgs.Length > 2 ? int.Parse(printerArgs[2]) : 9600;
            Console.WriteLine($"Using serial printer on {portName} @ {baudRate} baud...");
            return new SerialWoosimPrinter(portName, baudRate);
    }
}

static void RunTestPrint(IReceiptPrinter printer)
{
    printer.SetJustification(Justification.Center);
    printer.SetTextSize(2, 2);
    printer.SetBold(true);
    printer.Line("HELLO");
    printer.SetBold(false);
    printer.SetTextSize(1, 1);
    printer.Feed(1);

    printer.SetJustification(Justification.Left);
    printer.Line($"Printer test - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    printer.Line(new string('-', 32));
    printer.SetUnderline(true);
    printer.Line("Underlined line");
    printer.SetUnderline(false);
    printer.Line("Plain line");

    printer.Feed(3);
    printer.CutPaper();
}
