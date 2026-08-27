using System.Text.Json.Serialization;
using ReceiptPrinter.Printers.Network;
using ReceiptPrinter.Printers.Serial;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Service;
using ReceiptPrinter.Widgets;

// TODO: this project is a scaffold, not a finished service. It compiles and the endpoints below work,
// but hasn't been run against real hardware yet or hardened (no auth on the API, no retry/reconnect
// logic if the printer connection drops, etc.) - see README.md for the intended design.

var builder = WebApplication.CreateBuilder(args);

// When running as a Home Assistant add-on, Supervisor writes the user's configured add-on options to
// /data/options.json (snake_case keys, per the repo root's config.yaml) - merge those in
// so they can override the Printer:*/Briefing:* settings below without rebuilding the image.
builder.Configuration.AddJsonFile("/data/options.json", optional: true, reloadOnChange: false);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<IReceiptPrinter>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var printerType = config.GetValue("Printer:Type", "serial");
    var networkHost = config["printer_network_host"] ?? config.GetValue("Printer:NetworkHost", "printer.local")!;

    return printerType switch
    {
        "network" => new NetworkWoosimPrinter(networkHost),
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

app.MapPost("/briefing/trigger", async (IReceiptPrinter printer) =>
{
    var receipt = await DailyBriefing.BuildAsync();
    await printer.PrintAsync(receipt);
    return Results.Ok();
});

app.MapPost("/todos/check", async (TodoNoteChecker checker, IReceiptPrinter printer) =>
{
    await checker.CheckAndPrintAsync(printer);
    return Results.Ok();
});

app.Run();
