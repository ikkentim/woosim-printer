namespace ReceiptPrinter;

/// <summary>
/// Resolves where local config/state files (ha-config.json, todo.txt, todo-note-store.json, ...) live.
/// Defaults to next to the running executable (the old behaviour), but can be pointed at a persistent
/// volume via RECEIPTPRINTER_CONFIG_DIR - e.g. the Docker/Home Assistant add-on sets this to /data so
/// config survives container rebuilds instead of living inside the image's build output.
/// </summary>
public static class ConfigPaths
{
    public static string Directory { get; } =
        Environment.GetEnvironmentVariable("RECEIPTPRINTER_CONFIG_DIR") is { Length: > 0 } dir
            ? dir
            : AppContext.BaseDirectory;

    public static string Combine(string fileName) => Path.Combine(Directory, fileName);
}
