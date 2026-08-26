namespace ReceiptPrinter;

public enum Justification : byte { Left = 0, Center = 1, Right = 2 }

public enum CutMode : byte { Full = 0, Partial = 1 }

/// <summary>
/// A Woosim ESC/POS receipt printer, reachable either directly over serial or (eventually) over the network
/// via a standalone ESP32. Callers should code against this interface, not a specific transport.
/// </summary>
public interface IReceiptPrinter : IDisposable
{
    void Open();
    void Close();

    /// <summary>Resets the printer to its power-on defaults.</summary>
    void Initialize();

    void Text(string text);
    void Line(string text = "");
    void Feed(int lines = 1);
    void SetJustification(Justification justification);
    void SetBold(bool on);
    void SetUnderline(bool on);

    /// <summary>Width/height multiplier 1x-8x for the following text.</summary>
    void SetTextSize(int width = 1, int height = 1);

    void CutPaper(CutMode mode = CutMode.Full);
}
