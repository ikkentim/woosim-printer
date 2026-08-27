namespace ReceiptPrinter.Receipts;

/// <summary>
/// A Woosim ESC/POS receipt printer, reachable either directly over serial or (eventually) over the network
/// via a standalone ESP32. Callers hand over a whole <see cref="Receipt"/> and never touch printer-specific
/// commands or connection state - each implementation manages opening/closing its own connection internally.
/// </summary>
public interface IReceiptPrinter : IDisposable
{
    Task PrintAsync(Receipt receipt);

    /// <summary>
    /// Best-effort connectivity check that never prints anything - used to report a "printer reachable"
    /// status (e.g. as an MQTT binary_sensor) without wasting paper on a probe print.
    /// </summary>
    Task<bool> PingAsync();
}
