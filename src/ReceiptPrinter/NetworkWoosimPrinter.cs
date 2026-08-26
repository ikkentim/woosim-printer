namespace ReceiptPrinter;

/// <summary>
/// TODO: not yet available. Will drive a Woosim printer connected to a standalone ESP32 over WiFi/HTTP,
/// once the ESP32 firmware exists (see docs/HARDWARE.md for the hardware plan). The intended shape is
/// something like: buffer the same ESC/POS byte sequences the serial driver sends, then POST them as
/// the request body to e.g. http://{host}/print, with the ESP32 forwarding those bytes straight to the
/// printer's UART.
/// </summary>
public sealed class NetworkWoosimPrinter : IReceiptPrinter
{
    private readonly string _host;

    public NetworkWoosimPrinter(string host)
    {
        _host = host;
    }

    public void Open() => throw new NotImplementedException(
        $"Network printer at '{_host}' isn't implemented yet - the ESP32 firmware doesn't exist. Use the serial printer for now.");

    public void Close() => throw new NotImplementedException();
    public void Initialize() => throw new NotImplementedException();
    public void Text(string text) => throw new NotImplementedException();
    public void Line(string text = "") => throw new NotImplementedException();
    public void Feed(int lines = 1) => throw new NotImplementedException();
    public void SetJustification(Justification justification) => throw new NotImplementedException();
    public void SetBold(bool on) => throw new NotImplementedException();
    public void SetUnderline(bool on) => throw new NotImplementedException();
    public void SetTextSize(int width = 1, int height = 1) => throw new NotImplementedException();
    public void CutPaper(CutMode mode = CutMode.Full) => throw new NotImplementedException();

    public void Dispose()
    {
    }
}
