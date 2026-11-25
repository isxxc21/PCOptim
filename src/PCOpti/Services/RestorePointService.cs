using PCOpti.Models;

namespace PCOpti.Services;

public sealed record RestorePointResult(bool Success, string Message);

/// <summary>Creates a Windows System Restore point with Checkpoint-Computer.</summary>
public sealed class RestorePointService
{
    private readonly ICommandRunner _cmd;
    private readonly IChangeLog _log;

    public RestorePointService(ICommandRunner cmd, IChangeLog log)
    {
        _cmd = cmd; _log = log;
    }

    public RestorePointResult Create(string description)
    {
        var safe = description.Replace("'", "''");
        // Windows only allows one restore point per 24 hours by default; it then skips creation with a warning.
        var script = $$"""
            Checkpoint-Computer -Description '{{safe}}' -RestorePointType 'MODIFY_SETTINGS' -WarningVariable wv -WarningAction SilentlyContinue
            if ($wv) { Write-Output ('WARN:' + ($wv -join ' ')) } else { Write-Output 'OK' }
            """;
        var r = _cmd.RunPowerShell(script, 5 * 60_000);
        var output = r.StdOut.Trim();

        RestorePointResult result;
        if (r.Success && output.StartsWith("OK"))
            result = new(true, "Restore point created. You can return to it from Control Panel > Recovery > Open System Restore.");
        else if (r.Success && output.Contains("WARN:"))
            result = new(false, "Windows skipped creating a new restore point, because it only allows one every 24 hours " +
                                "and one was made recently. That recent restore point can still be used.");
        else if (r.ErrorText.Contains("disabled", StringComparison.OrdinalIgnoreCase) ||
                 r.ErrorText.Contains("0x80042302", StringComparison.OrdinalIgnoreCase) ||
                 r.ErrorText.Contains("service cannot be started", StringComparison.OrdinalIgnoreCase))
            result = new(false, "System Restore is turned off on this PC. To use it, open \"Create a restore point\" from " +
                                "the Start menu, select drive C: > Configure > Turn on system protection.");
        else
            result = new(false, $"Couldn't create a restore point: {r.ErrorText.Split('\n').FirstOrDefault()?.Trim()}");

        _log.Add("backup", result.Success ? "Created System Restore point" : "System Restore point not created",
            result.Success ? ChangeAction.Ran : ChangeAction.Info, new[] { result.Message });
        return result;
    }
}
