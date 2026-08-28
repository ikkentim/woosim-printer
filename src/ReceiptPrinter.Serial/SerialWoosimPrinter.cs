using System.IO.Ports;
using ReceiptPrinter.Printers;
using ReceiptPrinter.Receipts;

namespace ReceiptPrinter.Printers.Serial;

/// <summary>
/// Drives a Woosim receipt printer connected directly over serial (RS-232/USB-serial). All the
/// <see cref="Receipt"/> -> ESC/POS translation lives in <see cref="EscPosEncoder"/>; this class just
/// owns the serial connection and pushes the encoded bytes down it. The connection is opened on first
/// use and left open for reuse across multiple receipts.
/// </summary>
public sealed class SerialWoosimPrinter : IReceiptPrinter
{
    private readonly SerialPort _serial;

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
            _serial.Open();

        var data = EscPosEncoder.Encode(receipt);
        _serial.Write(data, 0, data.Length);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Serial has no real handshake to probe - this just checks the configured COM port is still
    /// enumerated by the OS (catches "USB-serial adapter unplugged", not "printer powered off").
    /// </summary>
    public Task<bool> PingAsync() =>
        Task.FromResult(SerialPort.GetPortNames()
            .Contains(_serial.PortName, StringComparer.OrdinalIgnoreCase));

    public void Dispose()
    {
        if (_serial.IsOpen)
            _serial.Close();
        _serial.Dispose();
    }
}
