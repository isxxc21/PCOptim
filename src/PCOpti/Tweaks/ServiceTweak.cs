using PCOpti.Models;

namespace PCOpti.Tweaks;

/// <summary>
/// Sets one curated Windows service to Manual (start on demand). Manual is not Disabled:
/// Windows can still start the service if something actually needs it.
/// The original start type is read from the registry and restored with sc.exe on revert.
/// </summary>
public abstract class ServiceTweak : TweakBase
{
    // HKLM\SYSTEM\CurrentControlSet\Services\<name>  Start: 2 = Automatic, 3 = Manual, 4 = Disabled.
    internal const int StartAuto = 2, StartManual = 3, StartDisabled = 4;

    protected ServiceTweak(TweakContext context) : base(context) { }

    /// <summary>Service key name, e.g. "Spooler".</summary>
    public abstract string ServiceName { get; }

    /// <summary>Start type used on revert if no backup exists. Defaults to Automatic.</summary>
    protected virtual int DefaultStart => StartAuto;
    protected virtual bool DefaultDelayed => false;

    public override TweakCategory Category => TweakCategory.Services;
    public override string Id => $"service.{ServiceName.ToLowerInvariant()}";

    private string KeyPath => $@"SYSTEM\CurrentControlSet\Services\{ServiceName}";

    public override string? GetUnavailableReason() =>
        Ctx.Registry.KeyExists(RegHive.LocalMachine, KeyPath) ? null : "This service isn't installed on this PC, so there is nothing to change.";

    internal int? CurrentStart => Ctx.Registry.GetValue(RegHive.LocalMachine, KeyPath, "Start")?.AsInt();
    private bool CurrentDelayed => Ctx.Registry.GetValue(RegHive.LocalMachine, KeyPath, "DelayedAutostart")?.AsInt() == 1;

    /// <summary>Applied when the service no longer starts automatically (Manual, or already Disabled).</summary>
    public override bool IsApplied() => CurrentStart is StartManual or StartDisabled;

    protected override IEnumerable<string> ApplyCore()
    {
        var start = CurrentStart ?? throw new TweakException("Couldn't read this service's start type.");
        if (start is StartManual or StartDisabled)
            return new[] { $"{ServiceName} already starts {Describe(start, false)}, so nothing was changed." };

        Ctx.Backups.TryAdd(new TweakBackup
        {
            Id = Id,
            DisplayName = Name,
            State = { ["Start"] = start.ToString(), ["Delayed"] = CurrentDelayed ? "1" : "0" },
        });

        try
        {
            RunSc("demand");
        }
        catch
        {
            Ctx.Backups.Remove(Id);
            throw;
        }
        return new[] { $"Service {ServiceName}: {Describe(start, CurrentDelayed)} -> Manual" };
    }

    protected override IEnumerable<string> RevertCore()
    {
        var backup = Ctx.Backups.Get(Id);
        int start = DefaultStart;
        bool delayed = DefaultDelayed;
        if (backup is not null && backup.State.TryGetValue("Start", out var s) && int.TryParse(s, out var parsed))
        {
            start = parsed;
            delayed = backup.State.TryGetValue("Delayed", out var d) && d == "1";
        }

        RunSc(ScStartArgument(start, delayed));
        Ctx.Backups.Remove(Id);
        return new[] { $"Service {ServiceName}: restored to {Describe(start, delayed)}{(backup is null ? " (Windows default, no backup found)" : "")}" };
    }

    internal static string ScStartArgument(int start, bool delayed) => start switch
    {
        StartAuto when delayed => "delayed-auto",
        StartAuto => "auto",
        StartDisabled => "disabled",
        _ => "demand",
    };

    private static string Describe(int start, bool delayed) => start switch
    {
        StartAuto when delayed => "Automatic (delayed start)",
        StartAuto => "Automatic",
        StartManual => "Manual",
        StartDisabled => "Disabled",
        _ => $"start type {start}",
    };

    private void RunSc(string startArg)
    {
        // sc.exe needs the literal "start= value" with a space after '='.
        var sc = Path.Combine(Environment.SystemDirectory, "sc.exe");
        var result = Ctx.Commands.Run(sc, $"config \"{ServiceName}\" start= {startArg}");
        if (result.Success) return;
        throw result.ExitCode switch
        {
            5 => new TweakException("Access denied by Windows. The service may be protected or locked by policy."),
            1060 => new TweakException("This service isn't installed on this PC."),
            _ => new TweakException($"sc.exe failed (code {result.ExitCode}): {result.ErrorText}"),
        };
    }
}
