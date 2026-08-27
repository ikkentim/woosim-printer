using System.CommandLine;
using ReceiptPrinter.Cli;
using ReceiptPrinter.Printers.Network;
using ReceiptPrinter.Printers.Serial;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Widgets;

var rootCommand = new RootCommand("Woosim receipt printer CLI");

rootCommand.Add(BuildPrintCommand("test", "Prints a basic ESC/POS test receipt.", () => Task.FromResult(BuildTestReceipt())));
rootCommand.Add(BuildPrintCommand("briefing", "Builds and prints the daily briefing.", DailyBriefing.BuildAsync));

var remindersDebugCommand = new Command("reminders-debug", "Lists Apple Reminders CalDAV lists and their contents, for debugging.");
remindersDebugCommand.SetAction(async (_, _) => await RemindersDebug.RunAsync());
rootCommand.Add(remindersDebugCommand);

return await rootCommand.Parse(args).InvokeAsync();

static Command BuildPrintCommand(string name, string description, Func<Task<Receipt>> buildReceipt)
{
    var printerOption = new Option<string>("--printer", "-p")
    {
        Description = "Which printer transport to use: 'serial' (default) or 'network'.",
        DefaultValueFactory = _ => "serial",
    };
    var portOption = new Option<string>("--port")
    {
        Description = "Serial port name (serial printer only).",
        DefaultValueFactory = _ => "COM3",
    };
    var baudOption = new Option<int>("--baud")
    {
        Description = "Serial baud rate (serial printer only).",
        DefaultValueFactory = _ => 9600,
    };
    var hostOption = new Option<string>("--host")
    {
        Description = "host:port of ReceiptPrinter.NetworkSerialService (network printer only).",
        DefaultValueFactory = _ => "printer.local:5251",
    };

    var command = new Command(name, description) { printerOption, portOption, baudOption, hostOption };

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        using var printer = CreatePrinter(
            parseResult.GetValue(printerOption)!,
            parseResult.GetValue(portOption)!,
            parseResult.GetValue(baudOption),
            parseResult.GetValue(hostOption)!);

        var receipt = await buildReceipt();
        await printer.PrintAsync(receipt);
        Console.WriteLine("Done.");
    });

    return command;
}

static IReceiptPrinter CreatePrinter(string printerType, string port, int baud, string host)
{
    switch (printerType)
    {
        case "network":
            Console.WriteLine($"Using network printer at {host}...");
            return new NetworkWoosimPrinter(host);

        case "serial":
            Console.WriteLine($"Using serial printer on {port} @ {baud} baud...");
            return new SerialWoosimPrinter(port, baud);

        default:
            throw new ArgumentException($"Unknown printer type '{printerType}' - expected 'serial' or 'network'.");
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
