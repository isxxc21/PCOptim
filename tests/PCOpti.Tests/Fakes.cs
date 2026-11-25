using PCOpti.Models;
using PCOpti.Services;
using PCOpti.Tweaks;

namespace PCOpti.Tests;

/// <summary>In-memory registry. Keys and value names are case-insensitive, like the real one.</summary>
public sealed class FakeRegistry : IRegistryAccess
{
    private readonly Dictionary<string, Dictionary<string, RegValue>> _keys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Value names that throw UnauthorizedAccessException when written (simulates Group Policy locks).</summary>
    public HashSet<string> DenyWrites { get; } = new(StringComparer.OrdinalIgnoreCase);

    private static string K(RegHive hive, string path) => $"{hive}\\{path.TrimEnd('\\')}";

    public RegValue? GetValue(RegHive hive, string path, string name) =>
        _keys.TryGetValue(K(hive, path), out var values) && values.TryGetValue(name, out var v) ? v : null;

    public void SetValue(RegHive hive, string path, string name, RegValue value)
    {
        if (DenyWrites.Contains(name)) throw new UnauthorizedAccessException($"Denied: {name}");
        if (!_keys.TryGetValue(K(hive, path), out var values))
            _keys[K(hive, path)] = values = new Dictionary<string, RegValue>(StringComparer.OrdinalIgnoreCase);
        values[name] = value;
    }

    public void DeleteValue(RegHive hive, string path, string name)
    {
        if (_keys.TryGetValue(K(hive, path), out var values)) values.Remove(name);
    }

    public bool KeyExists(RegHive hive, string path) => _keys.ContainsKey(K(hive, path));

    public IReadOnlyList<KeyValuePair<string, RegValue>> GetValues(RegHive hive, string path) =>
        _keys.TryGetValue(K(hive, path), out var values) ? values.ToList() : new List<KeyValuePair<string, RegValue>>();

    public void CreateKey(RegHive hive, string path)
    {
        if (!_keys.ContainsKey(K(hive, path)))
            _keys[K(hive, path)] = new Dictionary<string, RegValue>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Snapshot of everything, for "registry is exactly as before" assertions.</summary>
    public Dictionary<string, RegValue> Dump() =>
        _keys.SelectMany(k => k.Value.Select(v => (Key: $"{k.Key}\\{v.Key}".ToLowerInvariant(), v.Value)))
             .ToDictionary(x => x.Key, x => x.Value);
}

/// <summary>Records every command and answers with a scripted handler.</summary>
public sealed class FakeCommandRunner : ICommandRunner
{
    public List<string> Calls { get; } = new();
    public Func<string, string, CommandResult> Handler { get; set; } = (_, _) => new CommandResult(0, "", "");

    public CommandResult Run(string fileName, string arguments, int timeoutMs = 60000)
    {
        var exe = Path.GetFileName(fileName).ToLowerInvariant();
        Calls.Add($"{exe} {arguments}");
        return Handler(exe, arguments);
    }

    public CommandResult RunPowerShell(string script, int timeoutMs = 120000)
    {
        Calls.Add($"powershell {script}");
        return Handler("powershell", script);
    }
}

public sealed class FakeNotifier : ISystemNotifier
{
    public List<(int T1, int T2, int Speed)> MouseCalls { get; } = new();
    public List<string> Broadcasts { get; } = new();
    public void SetMouseAcceleration(int threshold1, int threshold2, int speed) => MouseCalls.Add((threshold1, threshold2, speed));
    public void BroadcastSettingChange(string area) => Broadcasts.Add(area);
}

public sealed class TestEnv
{
    public FakeRegistry Registry { get; } = new();
    public FakeCommandRunner Commands { get; } = new();
    public FakeNotifier Notifier { get; } = new();
    public InMemoryBackupStore Backups { get; } = new();
    public JsonChangeLog Log { get; } = new(null);

    public TweakContext Context => new(Registry, Backups, Log, Commands, Notifier);
}
