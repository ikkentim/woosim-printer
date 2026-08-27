namespace ReceiptPrinter;

/// <summary>
/// TODO: not yet available. Will drive a Woosim printer connected to a standalone ESP32 over WiFi/HTTP,
/// once the ESP32 firmware exists (see docs/HARDWARE.md for the hardware plan). The intended shape is
/// to serialize the Receipt's elements and POST them as the request body to e.g. http://{host}/print,
/// with the ESP32 rendering them (same logic as SerialWoosimPrinter.Render, just running on the ESP32
/// instead) and forwarding the resulting bytes straight to the printer's UART.
/// </summary>
public sealed class NetworkWoosimPrinter : IReceiptPrinter
{
    private readonly string _host;

    public NetworkWoosimPrinter(string host)
    {
        _host = host;
    }

    public Task PrintAsync(Receipt receipt) => throw new NotImplementedException(
        $"Network printer at '{_host}' isn't implemented yet - the ESP32 firmware doesn't exist. Use the serial printer for now.");

    public void Dispose()
    {
    }
}
