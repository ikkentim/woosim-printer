namespace ReceiptPrinter;

/// <summary>
/// A Woosim ESC/POS receipt printer, reachable either directly over serial or (eventually) over the network
/// via a standalone ESP32. Callers hand over a whole <see cref="Receipt"/> and never touch printer-specific
/// commands or connection state - each implementation manages opening/closing its own connection internally.
/// </summary>
public interface IReceiptPrinter : IDisposable
{
    Task PrintAsync(Receipt receipt);
}
