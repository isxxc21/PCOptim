namespace PCOpti.Models;

/// <summary>
/// The original state of everything one tweak changed, captured just before it first ran.
/// Persisted to %LocalAppData%\PCOpti\backups.json so Revert works across restarts.
/// </summary>
public sealed class TweakBackup
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Original registry values (for registry-based tweaks).</summary>
    public List<RegistrySnapshot> Registry { get; set; } = new();

    /// <summary>Free-form original state for non-registry changes (power plan GUID, service start type...).</summary>
    public Dictionary<string, string> State { get; set; } = new();
}

/// <summary>One registry value as it was before PCOpti changed it.</summary>
public sealed class RegistrySnapshot
{
    public RegHive Hive { get; set; }
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>False if the value did not exist; revert then deletes it again.</summary>
    public bool Existed { get; set; }
    public RegValue? Value { get; set; }

    public string Location => $"{RegValue.HiveName(Hive)}\\{Path}\\{Name}";
}
