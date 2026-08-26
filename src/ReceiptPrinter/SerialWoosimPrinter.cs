using System.IO.Ports;
using System.Text;

namespace ReceiptPrinter;

/// <summary>
/// Minimal ESC/POS driver for a Woosim receipt printer connected directly over serial (RS-232/USB-serial).
/// </summary>
public sealed class SerialWoosimPrinter : IReceiptPrinter
{
    private const byte ESC = 0x1B;
    private const byte GS = 0x1D;

    private readonly SerialPort _serial;

    public SerialWoosimPrinter(string portName, int baudRate = 9600)
    {
        _serial = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            WriteTimeout = 5000,
        };
    }

    public void Open() => _serial.Open();

    public void Close() => _serial.Close();

    public void Initialize() => Send(ESC, (byte)'@');

    public void Text(string text)
    {
        // Woosim printers default to code page 437 / plain ASCII for latin text.
        var bytes = Encoding.GetEncoding(437).GetBytes(text);
        Send(bytes);
    }

    public void Line(string text = "") => Text(text + "\n");

    public void Feed(int lines = 1)
    {
        for (var i = 0; i < lines; i++)
            Send((byte)'\n');
    }

    public void SetJustification(Justification justification) =>
        Send(ESC, (byte)'a', (byte)justification);

    public void SetBold(bool on) =>
        Send(ESC, (byte)'E', (byte)(on ? 1 : 0));

    public void SetUnderline(bool on) =>
        Send(ESC, (byte)'-', (byte)(on ? 1 : 0));

    public void SetTextSize(int width = 1, int height = 1)
    {
        width = Math.Clamp(width, 1, 8);
        height = Math.Clamp(height, 1, 8);
        var n = (byte)(((width - 1) << 4) | (height - 1));
        Send(GS, (byte)'!', n);
    }

    public void CutPaper(CutMode mode = CutMode.Full) =>
        Send(GS, (byte)'V', (byte)mode);

    private void Send(params byte[] data) => _serial.Write(data, 0, data.Length);

    public void Dispose()
    {
        if (_serial.IsOpen)
            _serial.Close();
        _serial.Dispose();
    }
}
