using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.Printers.Network;
using ReceiptPrinter.Printers.Serial;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Service;
using ReceiptPrinter.Widgets;

// TODO: this project is a scaffold, not a finished service. It compiles and the endpoints below work,
// but hasn't been run against real hardware yet or hardened (no auth on the API, no retry/reconnect
// logic if the printer connection drops, etc.) - see README.md for the intended design.

var builder = WebApplication.CreateBuilder(args);

// Same config layering as the CLI (see ReceiptPrinterConfiguration) plus, when running as a Home
// Assistant add-on, Supervisor's /data/options.json - reloads live, so editing the add-on's
// Configuration tab takes effect without a restart.
builder.Configuration.AddJsonFile(ConfigPaths.Combine("appsettings.local.json"), optional: true);
builder.Configuration.AddJsonFile("/data/options.json", optional: true, reloadOnChange: true);

builder.Services.Configure<ReceiptPrinterOptions>(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<IReceiptPrinter>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var printerType = config.GetValue("Printer:Type", "serial");

    return printerType switch
    {
        "network" => new NetworkWoosimPrinter(config.GetValue("Printer:NetworkHost", "printer.local")!),
        _ => new SerialWoosimPrinter(
            config.GetValue("Printer:Port", "COM3")!,
            config.GetValue("Printer:Baud", 9600)),
    };
});

builder.Services.AddSingleton<TodoNoteStore>();
builder.Services.AddSingleton<TodoNoteChecker>();
builder.Services.AddHostedService<BriefingScheduler>();

var app = builder.Build();

app.MapPost("/print", async (Receipt receipt, IReceiptPrinter printer) =>
{
    await printer.PrintAsync(receipt);
    return Results.Ok();
});

app.MapPost("/briefing/trigger", async (IReceiptPrinter printer, IOptionsSnapshot<ReceiptPrinterOptions> options) =>
{
    var receipt = await DailyBriefing.BuildAsync(options.Value);
    await printer.PrintAsync(receipt);
    return Results.Ok();
});

app.MapPost("/todos/check", async (TodoNoteChecker checker, IReceiptPrinter printer) =>
{
    await checker.CheckAndPrintAsync(printer);
    return Results.Ok();
});

app.Run();
