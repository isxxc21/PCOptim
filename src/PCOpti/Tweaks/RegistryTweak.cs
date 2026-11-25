using PCOpti.Models;

namespace PCOpti.Tweaks;

/// <summary>
/// One registry value a tweak sets.
/// <paramref name="RevertTo"/> is only used when no backup exists (for example the value was already set
/// before PCOpti was installed). Null means "delete the value", which restores Windows' built-in default.
/// </summary>
public sealed record RegistrySetting(RegHive Hive, string Path, string Name, RegValue Applied, RegValue? RevertTo)
{
    public string Location => $"{RegValue.HiveName(Hive)}\\{Path}\\{Name}";
}

/// <summary>
/// Base for tweaks that are just a set of registry values. Handles detection, backup,
/// all-or-nothing apply (rolls back if any write fails) and exact restore.
/// </summary>
public abstract class RegistryTweak : TweakBase
{
    protected RegistryTweak(TweakContext context) : base(context) { }

    /// <summary>The values this tweak writes.</summary>
    public abstract IReadOnlyList<RegistrySetting> Settings { get; }

    /// <summary>Applied only when every value equals its target.</summary>
    public override bool IsApplied() =>
        Settings.All(s => Equals(Ctx.Registry.GetValue(s.Hive, s.Path, s.Name), s.Applied));

    protected override IEnumerable<string> ApplyCore()
    {
        // 1. Snapshot the current values. TryAdd keeps an older backup if one exists,
        //    so re-applying never overwrites the true original.
        var snapshot = Settings.Select(s =>
        {
            var current = Ctx.Registry.GetValue(s.Hive, s.Path, s.Name);
            return new RegistrySnapshot { Hive = s.Hive, Path = s.Path, Name = s.Name, Existed = current is not null, Value = current };
        }).ToList();
        var addedBackup = Ctx.Backups.TryAdd(new TweakBackup { Id = Id, DisplayName = Name, Registry = snapshot });

        // 2. Write the new values; if one fails, put back what we already changed.
        var details = new List<string>();
        var written = new List<RegistrySnapshot>();
        try
        {
            foreach (var s in Settings)
            {
                var before = snapshot.First(x => x.Path == s.Path && x.Name == s.Name && x.Hive == s.Hive);
                Ctx.Registry.SetValue(s.Hive, s.Path, s.Name, s.Applied);
                written.Add(before);
                details.Add($"{s.Location}: {(before.Existed ? before.Value!.ToString() : "(not set)")} -> {s.Applied}");
            }
        }
        catch
        {
            foreach (var w in written) TryRestore(w);
            // Nothing is left changed, so a backup we just created would be misleading.
            if (addedBackup) Ctx.Backups.Remove(Id);
            throw;
        }

        AfterChange();
        return details;
    }

    protected override IEnumerable<string> RevertCore()
    {
        var backup = Ctx.Backups.Get(Id);
        var details = new List<string>();

        foreach (var s in Settings)
        {
            var snap = backup?.Registry.FirstOrDefault(x => x.Hive == s.Hive && x.Path == s.Path && x.Name == s.Name);
            if (snap is not null)
            {
                Restore(snap);
                details.Add($"{s.Location}: restored {(snap.Existed ? snap.Value!.ToString() : "(deleted, Windows default)")}");
            }
            else if (s.RevertTo is not null)
            {
                Ctx.Registry.SetValue(s.Hive, s.Path, s.Name, s.RevertTo);
                details.Add($"{s.Location}: set to default {s.RevertTo} (no backup found)");
            }
            else
            {
                Ctx.Registry.DeleteValue(s.Hive, s.Path, s.Name);
                details.Add($"{s.Location}: deleted to restore Windows default (no backup found)");
            }
        }

        Ctx.Backups.Remove(Id);
        AfterChange();
        return details;
    }

    /// <summary>Hook to make Windows pick up the change live (e.g. broadcast WM_SETTINGCHANGE).</summary>
    protected virtual void AfterChange() { }

    private void Restore(RegistrySnapshot snap)
    {
        if (snap.Existed && snap.Value is not null)
            Ctx.Registry.SetValue(snap.Hive, snap.Path, snap.Name, snap.Value);
        else
            Ctx.Registry.DeleteValue(snap.Hive, snap.Path, snap.Name);
    }

    private void TryRestore(RegistrySnapshot snap)
    {
        try { Restore(snap); } catch { /* best effort during rollback */ }
    }
}
