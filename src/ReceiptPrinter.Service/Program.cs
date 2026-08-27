using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReceiptPrinter.Configuration;
using ReceiptPrinter.HomeAssistant;
using ReceiptPrinter.Printers.Network;
using ReceiptPrinter.Printers.Serial;
using ReceiptPrinter.Receipts;
using ReceiptPrinter.Service;

// A plain background worker, no HTTP server - every action (print the briefing, check to-dos, print a
// freeform message) is triggered over MQTT via MqttAddonService. See README.md's "MQTT entities"
// section for the automation-facing side of this.

var builder = Host.CreateApplicationBuilder(args);

// Same config layering as the CLI (see ReceiptPrinterConfiguration) plus, when running as a Home
// Assistant add-on, Supervisor's /data/options.json - reloads live, so editing the add-on's
// Configuration tab takes effect without a restart.
builder.Configuration.AddJsonFile(ConfigPaths.Combine("appsettings.local.json"), optional: true);
builder.Configuration.AddJsonFile("/data/options.json", optional: true, reloadOnChange: true);

builder.Services.Configure<ReceiptPrinterOptions>(builder.Configuration);

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
builder.Services.AddHostedService<ReceiptPrinter.Service.Mqtt.MqttAddonService>();

var app = builder.Build();

// Logs whether Home Assistant connectivity resolved (and from where), without ever exposing the token
// value - since there's no /diag HTTP endpoint anymore, this is the equivalent for the "briefing comes
// back with sections missing" debugging case: check the add-on's log for this line.
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var haOptions = app.Services.GetRequiredService<IOptions<ReceiptPrinterOptions>>().Value.HomeAssistant;
var connection = HomeAssistantConnection.Resolve(haOptions);
logger.LogInformation(
    "Home Assistant connectivity: {Status} (source: {Source})",
    connection != null ? "resolved" : "unavailable",
    connection == null ? "none" : !string.IsNullOrEmpty(haOptions.BaseUrl) ? "explicit BaseUrl/Token" : "Supervisor token");

app.Run();
