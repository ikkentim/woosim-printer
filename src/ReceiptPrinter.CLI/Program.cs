using ReceiptPrinter.Cli;
using ReceiptPrinter.Printers.Network;
using ReceiptPrinter.Printers.Serial;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Widgets;

// Usage:
//   dotnet run -- <command> [printer-type] [printer-args...]
//   command:      test | briefing | reminders-debug
//   printer-type: serial (default) [port=COM3] [baud=9600]
//                 network           [host]      (talks to ReceiptPrinter.NetworkSerialService)
var command = args.Length > 0 ? args[0] : "test";

if (command == "reminders-debug")
{
    await RemindersDebug.RunAsync();
    return;
}

using var printer = CreatePrinter(args.Skip(1).ToArray());

var receipt = command switch
{
    "briefing" => await DailyBriefing.BuildAsync(),
    _ => BuildTestReceipt(),
};

await printer.PrintAsync(receipt);
Console.WriteLine("Done.");

static IReceiptPrinter CreatePrinter(string[] printerArgs)
{
    var printerType = printerArgs.Length > 0 ? printerArgs[0] : "serial";

    switch (printerType)
    {
        case "network":
            var host = printerArgs.Length > 1 ? printerArgs[1] : "printer.local:5251";
            Console.WriteLine($"Using network printer at {host}...");
            return new NetworkWoosimPrinter(host);

        case "serial":
        default:
            var portName = printerArgs.Length > 1 ? printerArgs[1] : "COM3";
            var baudRate = printerArgs.Length > 2 ? int.Parse(printerArgs[2]) : 9600;
            Console.WriteLine($"Using serial printer on {portName} @ {baudRate} baud...");
            return new SerialWoosimPrinter(portName, baudRate);
    }
}

static Receipt BuildTestReceipt()
{
    IReadOnlyList<IElement> elements =
    [
        new TextElement("HELLO", Bold: true, Width: 2, Height: 2, Justification: Justification.Center),
        new TextElement(""),
        new TextElement($"Printer test - {DateTime.Now:yyyy-MM-dd HH:mm:ss}"),
        new TextElement(new string('-', 32)),
        new TextElement("Underlined line", Underline: true),
        new TextElement("Plain line"),
        new TextElement(""),
        new TextElement(""),
        new TextElement(""),
    ];

    return new Receipt(elements, CutStyle.Partial);
}
