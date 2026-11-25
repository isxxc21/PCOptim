using System.Text.RegularExpressions;
using PCOpti.Models;

namespace PCOpti.Tweaks;

/// <summary>
/// Switches the active power plan. Remembers the plan that was active before so revert goes back to it.
/// If the plan is missing (common for Ultimate Performance) it is created with powercfg -duplicatescheme,
/// and that copy is deleted again on revert.
/// </summary>
public abstract partial class PowerPlanTweak : TweakBase
{
    public const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    protected PowerPlanTweak(TweakContext context) : base(context) { }

    /// <summary>The built-in scheme GUID for this plan.</summary>
    protected abstract string SchemeGuid { get; }

    public override TweakCategory Category => TweakCategory.Gaming;

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    private static string PowerCfg => Path.Combine(Environment.SystemDirectory, "powercfg.exe");

    /// <summary>Extracts the first GUID from powercfg output ("Power Scheme GUID: xxxx  (Balanced)").</summary>
    internal static string? ParseGuid(string output)
    {
        var m = GuidRegex().Match(output);
        return m.Success ? m.Value.ToLowerInvariant() : null;
    }

    internal string? GetActiveScheme()
    {
        var r = Ctx.Commands.Run(PowerCfg, "/getactivescheme");
        return r.Success ? ParseGuid(r.StdOut) : null;
    }

    private bool SchemeExists(string guid)
    {
        var r = Ctx.Commands.Run(PowerCfg, "/list");
        return r.Success && r.StdOut.Contains(guid, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The GUID we switch to: the copy we created earlier if any, else the built-in GUID.</summary>
    private string TargetScheme =>
        Ctx.Backups.Get(Id)?.State.GetValueOrDefault("Target") ?? SchemeGuid;

    public override bool IsApplied() =>
        string.Equals(GetActiveScheme(), TargetScheme, StringComparison.OrdinalIgnoreCase);

    protected override IEnumerable<string> ApplyCore()
    {
        var original = GetActiveScheme() ?? throw new TweakException("Couldn't read the current power plan (powercfg failed).");
        var existing = Ctx.Backups.Get(Id);
        var target = existing?.State.GetValueOrDefault("Target") ?? SchemeGuid;
        var created = false;

        if (!SchemeExists(target))
        {
            // Plan is hidden or missing: create a copy from Windows' built-in template.
            var dup = Ctx.Commands.Run(PowerCfg, $"-duplicatescheme {SchemeGuid}");
            target = dup.Success ? ParseGuid(dup.StdOut) ?? "" : "";
            if (target.Length == 0)
                throw new TweakException(
                    "Windows wouldn't create this power plan. On many laptops with Modern Standby only the Balanced plan is " +
                    "allowed; use Settings > System > Power > Power mode > Best performance instead.");
            created = true;
        }

        if (existing is null)
        {
            Ctx.Backups.TryAdd(new TweakBackup
            {
                Id = Id,
                DisplayName = Name,
                State = { ["Original"] = original, ["Target"] = target, ["Created"] = created ? "1" : "0" },
            });
        }
        else if (created)
        {
            // Our earlier copy was deleted outside PCOpti; keep the original plan but record the new copy.
            var updated = new TweakBackup
            {
                Id = Id,
                DisplayName = Name,
                CreatedUtc = existing.CreatedUtc,
                State = new Dictionary<string, string>(existing.State) { ["Target"] = target, ["Created"] = "1" },
            };
            Ctx.Backups.Remove(Id);
            Ctx.Backups.TryAdd(updated);
        }

        var set = Ctx.Commands.Run(PowerCfg, $"/setactive {target}");
        if (!set.Success) throw new TweakException($"powercfg couldn't switch plan: {set.ErrorText}");

        return new[] { $"Active power plan: {original} -> {target}{(created ? " (created from Windows template)" : "")}" };
    }

    protected override IEnumerable<string> RevertCore()
    {
        var backup = Ctx.Backups.Get(Id);
        var original = backup?.State.GetValueOrDefault("Original") ?? BalancedGuid;
        var target = backup?.State.GetValueOrDefault("Target") ?? SchemeGuid;
        var details = new List<string>();

        // Only switch back if our plan is still active; if the user (or another tweak) picked a different
        // plan since, leave their choice alone.
        if (string.Equals(GetActiveScheme(), target, StringComparison.OrdinalIgnoreCase))
        {
            if (!SchemeExists(original)) original = BalancedGuid;
            var set = Ctx.Commands.Run(PowerCfg, $"/setactive {original}");
            if (!set.Success) throw new TweakException($"powercfg couldn't switch plan back: {set.ErrorText}");
            details.Add($"Active power plan restored to {original}");
        }
        else
        {
            details.Add("This plan wasn't active any more, so the current plan was left as it is.");
        }

        if (backup?.State.GetValueOrDefault("Created") == "1")
        {
            Ctx.Commands.Run(PowerCfg, $"-delete {target}");
            details.Add($"Removed the plan copy PCOpti created ({target})");
        }

        Ctx.Backups.Remove(Id);
        return details;
    }
}
