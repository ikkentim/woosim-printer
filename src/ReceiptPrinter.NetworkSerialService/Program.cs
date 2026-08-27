using System.Text.Json.Serialization;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Printers.Serial;

// Stands in for the ESP32 firmware planned in docs/HARDWARE.md. This runs on the PC that has the
// Woosim printer physically wired up over serial (COM3), and exposes the same tiny HTTP contract
// NetworkWoosimPrinter speaks (POST /print, a Receipt as JSON) - so ReceiptPrinter.Service can run
// wherever's convenient (e.g. a Home Assistant add-on) and print to this machine over the network,
// without knowing there's no ESP32 behind it yet. Once the firmware exists, it just needs to speak the
// same protocol and this project goes away.

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<IReceiptPrinter>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new SerialWoosimPrinter(
        config.GetValue("Printer:Port", "COM3")!,
        config.GetValue("Printer:Baud", 9600));
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.MapPost("/print", async (Receipt receipt, IReceiptPrinter printer) =>
{
    await printer.PrintAsync(receipt);
    return Results.Ok();
});

app.Run();
