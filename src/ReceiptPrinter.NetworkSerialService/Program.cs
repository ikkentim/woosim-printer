using System.IO.Ports;

// Stands in for the ESP32 firmware planned in docs/HARDWARE.md. Runs on the PC that has the Woosim
// printer physically wired up over serial (COM3) and does exactly what the firmware will: accept an
// HTTP POST carrying a raw ESC/POS byte stream and copy it straight to the serial port. All the
// Receipt -> ESC/POS translation happens on the sender (NetworkWoosimPrinter / EscPosEncoder), so
// this side needs no knowledge of receipts, JSON, or ESC/POS - just "HTTP body in, serial out".
// Once the firmware exists it just needs to speak the same trivial protocol and this project goes away.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var portName = app.Configuration.GetValue("Printer:Port", "COM3")!;
var baudRate = app.Configuration.GetValue("Printer:Baud", 9600);

var serial = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
{
    Handshake = Handshake.None,
    WriteTimeout = 5000,
};

// One physical printer, one port - serialize concurrent POSTs so two jobs can't interleave on the wire.
var gate = new SemaphoreSlim(1, 1);

app.MapGet("/health", () => Results.Ok("ok"));

app.MapPost("/print", async (HttpRequest request) =>
{
    await gate.WaitAsync();
    try
    {
        if (!serial.IsOpen)
            serial.Open();

        await request.Body.CopyToAsync(serial.BaseStream);
    }
    finally
    {
        gate.Release();
    }

    return Results.Ok();
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    if (serial.IsOpen)
        serial.Close();
    serial.Dispose();
});

app.Run();
