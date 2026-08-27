namespace ReceiptPrinter.Configuration;

/// <summary>
/// A plain-text to-do list (one item per line) - the fallback the to-do widget uses when Home Assistant
/// isn't configured. Genuinely free-form user content rather than a setting, so it stays a file (see
/// ConfigPaths) instead of moving into ReceiptPrinterOptions.
/// </summary>
public static class TodoFile
{
    private static readonly string Path = ConfigPaths.Combine("todo.txt");

    public static List<string> Load()
    {
        if (!File.Exists(Path))
        {
            File.WriteAllText(Path, "");
            return [];
        }

        return File.ReadAllLines(Path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }
}
