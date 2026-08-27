using System.IO.Ports;
using System.Text;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Printers.Serial;

/// <summary>
/// Drives a Woosim receipt printer connected directly over serial (RS-232/USB-serial), translating
/// <see cref="Receipt"/> elements into ESC/POS commands. Callers just call <see cref="PrintAsync"/> -
/// the serial connection is opened on first use and left open for reuse across multiple receipts.
/// </summary>
public sealed class SerialWoosimPrinter : IReceiptPrinter
{
    private const byte ESC = 0x1B;
    private const byte GS = 0x1D;

    private readonly SerialPort _serial;

    static SerialWoosimPrinter()
    {
        // Woosim printers speak plain code page 437 for latin text - .NET Core needs this registered
        // explicitly to resolve it.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public SerialWoosimPrinter(string portName, int baudRate = 9600)
    {
        _serial = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            WriteTimeout = 5000,
        };
    }

    public Task PrintAsync(Receipt receipt)
    {
        if (!_serial.IsOpen)
        {
            _serial.Open();
            Initialize();
        }

        foreach (var element in receipt.Elements)
        {
            switch (element)
            {
                case TextElement text:
                    Render(text);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported element type: {element.GetType().Name}");
            }
        }

        CutPaper(receipt.Cut);

        return Task.CompletedTask;
    }

    private void Render(TextElement element)
    {
        SetJustification(element.Justification);
        SetTextSize(element.Width, element.Height);
        SetBold(element.Bold);
        SetUnderline(element.Underline);

        if (element.LineBreak)
            Line(element.Text);
        else
            Text(element.Text);
    }

    /// <summary>
    /// Serial has no real handshake to probe - this just checks the configured COM port is still
    /// enumerated by the OS (catches "USB-serial adapter unplugged", not "printer powered off").
    /// </summary>
    public Task<bool> PingAsync() =>
        Task.FromResult(System.IO.Ports.SerialPort.GetPortNames()
            .Contains(_serial.PortName, StringComparer.OrdinalIgnoreCase));

    private void Initialize() => Send(ESC, (byte)'@');

    private void Text(string text)
    {
        var bytes = Encoding.GetEncoding(437).GetBytes(text);
        Send(bytes);
    }

    private void Line(string text) => Text(text + "\n");

    private void SetJustification(Justification justification) =>
        Send(ESC, (byte)'a', (byte)justification);

    private void SetBold(bool on) =>
        Send(ESC, (byte)'E', (byte)(on ? 1 : 0));

    private void SetUnderline(bool on) =>
        Send(ESC, (byte)'-', (byte)(on ? 1 : 0));

    private void SetTextSize(int width, int height)
    {
        width = Math.Clamp(width, 1, 8);
        height = Math.Clamp(height, 1, 8);
        var n = (byte)(((width - 1) << 4) | (height - 1));
        Send(GS, (byte)'!', n);
    }

    private void CutPaper(CutStyle style) =>
        Send(GS, (byte)'V', (byte)style);

    private void Send(params byte[] data) => _serial.Write(data, 0, data.Length);

    public void Dispose()
    {
        if (_serial.IsOpen)
            _serial.Close();
        _serial.Dispose();
    }
}
