using System.Text.Json;
using PCOpti.Models;

namespace PCOpti.Services;

public interface IChangeLog
{
    void Add(ChangeEntry entry);
    IReadOnlyList<ChangeEntry> GetAll();
    void Clear();
    event EventHandler? Changed;
}

public static class ChangeLogExtensions
{
    public static void Add(this IChangeLog log, string source, string title, ChangeAction action, IEnumerable<string>? details = null) =>
        log.Add(new ChangeEntry { Source = source, Title = title, Action = action, Details = details?.ToList() ?? new() });
}

/// <summary>Change log persisted as JSON, newest first, capped so the file stays small.</summary>
public sealed class JsonChangeLog : IChangeLog
{
    private const int MaxEntries = 3000;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string? _path;
    private readonly object _gate = new();
    private readonly List<ChangeEntry> _entries = new();

    public event EventHandler? Changed;

    /// <param name="path">File to persist to, or null for an in-memory log (tests).</param>
    public JsonChangeLog(string? path)
    {
        _path = path;
        if (path is null || !File.Exists(path)) return;
        try
        {
            _entries = JsonSerializer.Deserialize<List<ChangeEntry>>(File.ReadAllText(path)) ?? new();
        }
        catch
        {
            // Unreadable log: start fresh, it's informational only.
        }
    }

    public void Add(ChangeEntry entry)
    {
        lock (_gate)
        {
            _entries.Insert(0, entry);
            if (_entries.Count > MaxEntries) _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            Save();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<ChangeEntry> GetAll()
    {
        lock (_gate) return _entries.ToList();
    }

    public void Clear()
    {
        lock (_gate) { _entries.Clear(); Save(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        if (_path is null) return;
        try
        {
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, Json));
            File.Move(tmp, _path, overwrite: true);
        }
        catch
        {
            // Failing to write the log must not break a tweak (the backup file is what matters).
        }
    }
}
