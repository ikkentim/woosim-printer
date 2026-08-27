using System.CommandLine;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.Printers.Network;
using ReceiptPrinter.Printers.Serial;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Widgets;

var rootCommand = new RootCommand("Woosim receipt printer CLI");

rootCommand.Add(BuildPrintCommand("test", "Prints a basic ESC/POS test receipt.", extra: [],
    (_, _) => Task.FromResult(BuildTestReceipt())));
rootCommand.Add(BuildPrintCommand("briefing", "Builds and prints the daily briefing.", extra: [],
    (_, _) => DailyBriefing.BuildAsync(ReceiptPrinterConfiguration.Load())));

var messageArgument = new Argument<string?>("message")
{
    Description = "Text to print, using ReceiptMarkdown formatting (see README.md). Omit to read from stdin instead.",
    Arity = ArgumentArity.ZeroOrOne,
};
rootCommand.Add(BuildPrintCommand("print", "Prints text (an argument, or piped via stdin) using ReceiptMarkdown formatting.",
    extra: [messageArgument], async (parseResult, cancellationToken) =>
    {
        var message = parseResult.GetValue(messageArgument);
        if (string.IsNullOrEmpty(message))
        {
            if (!Console.IsInputRedirected)
                throw new InvalidOperationException("Provide text as an argument, or pipe it via stdin (e.g. `echo hello | dotnet run -- print`).");

            message = await Console.In.ReadToEndAsync(cancellationToken);
        }

        var options = ReceiptPrinterConfiguration.Load();
        Localization.SetLanguage(options.Briefing.Language);
        var widgetFactories = DailyBriefingWidget.CreateWidgetFactories(options);

        return await ReceiptMarkdown.ParseAsync(message, async name =>
        {
            if (widgetFactories.TryGetValue(name, out var factory))
                return await factory().RenderAsync();

            Console.WriteLine($"Unknown widget '{name}' referenced, skipping.");
            return Array.Empty<IElement>();
        });
    }));

return await rootCommand.Parse(args).InvokeAsync();

static Command BuildPrintCommand(string name, string description, Argument[] extra, Func<ParseResult, CancellationToken, Task<Receipt>> buildReceipt)
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
    foreach (var argument in extra)
        command.Add(argument);

    command.SetAction(async (parseResult, cancellationToken) =>
    {
        using var printer = CreatePrinter(
            parseResult.GetValue(printerOption)!,
            parseResult.GetValue(portOption)!,
            parseResult.GetValue(baudOption),
            parseResult.GetValue(hostOption)!);

        var receipt = await buildReceipt(parseResult, cancellationToken);
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
