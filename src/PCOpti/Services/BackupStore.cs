using System.Text.Json;
using PCOpti.Models;

namespace PCOpti.Services;

public interface IBackupStore
{
    /// <summary>
    /// Stores a backup unless one already exists for this id. Returns false if one existed:
    /// the FIRST snapshot is the true original state, so it is never overwritten.
    /// </summary>
    bool TryAdd(TweakBackup backup);
    TweakBackup? Get(string id);
    void Remove(string id);
    IReadOnlyList<TweakBackup> GetAll();
    event EventHandler? Changed;
}

/// <summary>Thread-safe backup store persisted as JSON. Writes are atomic (temp file + rename).</summary>
public sealed class JsonBackupStore : IBackupStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;
    private readonly object _gate = new();
    private readonly Dictionary<string, TweakBackup> _items;

    public event EventHandler? Changed;

    public JsonBackupStore(string path)
    {
        _path = path;
        _items = Load(path);
    }

    public bool TryAdd(TweakBackup backup)
    {
        lock (_gate)
        {
            if (_items.ContainsKey(backup.Id)) return false;
            _items[backup.Id] = backup;
            Save();
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public TweakBackup? Get(string id)
    {
        lock (_gate) return _items.TryGetValue(id, out var b) ? b : null;
    }

    public void Remove(string id)
    {
        lock (_gate)
        {
            if (!_items.Remove(id)) return;
            Save();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<TweakBackup> GetAll()
    {
        lock (_gate) return _items.Values.OrderBy(b => b.CreatedUtc).ToList();
    }

    private void Save()
    {
        // Backups are critical: let a write failure surface so the tweak is NOT applied without one.
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_items.Values.ToList(), Json));
        File.Move(tmp, _path, overwrite: true);
    }

    private static Dictionary<string, TweakBackup> Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new();
            var list = JsonSerializer.Deserialize<List<TweakBackup>>(File.ReadAllText(path)) ?? new();
            return list.Where(b => !string.IsNullOrEmpty(b.Id)).GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());
        }
        catch (Exception)
        {
            // Never silently lose backups: keep the unreadable file for manual recovery.
            try { File.Copy(path, path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { }
            return new();
        }
    }
}

/// <summary>In-memory store, used by unit tests.</summary>
public sealed class InMemoryBackupStore : IBackupStore
{
    private readonly Dictionary<string, TweakBackup> _items = new();
    public event EventHandler? Changed;

    public bool TryAdd(TweakBackup backup)
    {
        if (!_items.TryAdd(backup.Id, backup)) return false;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public TweakBackup? Get(string id) => _items.TryGetValue(id, out var b) ? b : null;

    public void Remove(string id)
    {
        if (_items.Remove(id)) Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<TweakBackup> GetAll() => _items.Values.ToList();
}
