using System.Text.Json;
using ReceiptPrinter.Configuration;

namespace ReceiptPrinter.Service;

/// <summary>
/// Remembers which to-do items have already been printed as their own note, so the checker only
/// prints genuinely new ones and can tell when one has disappeared from the source.
/// </summary>
public sealed class TodoNoteStore
{
    private readonly string _path = ConfigPaths.Combine("todo-note-store.json");

    public HashSet<string> Load()
    {
        if (!File.Exists(_path))
            return new HashSet<string>();

        var items = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_path));
        return new HashSet<string>(items ?? Array.Empty<string>());
    }

    public void Save(IEnumerable<string> items) =>
        File.WriteAllText(_path, JsonSerializer.Serialize(items.ToArray(), new JsonSerializerOptions { WriteIndented = true }));
}
