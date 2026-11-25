using PCOpti.Models;
using PCOpti.Tweaks;

namespace PCOpti.Services;

public sealed record RevertAllResult(int Reverted, List<string> Failures);

/// <summary>Undoes every change PCOpti still holds a backup for: tweaks, startup items and DNS.</summary>
public sealed class RevertAllService
{
    private readonly IReadOnlyList<ITweak> _tweaks;
    private readonly IBackupStore _backups;
    private readonly StartupService _startup;
    private readonly NetworkService _network;
    private readonly IChangeLog _log;

    public RevertAllService(IReadOnlyList<ITweak> tweaks, IBackupStore backups, StartupService startup, NetworkService network, IChangeLog log)
    {
        _tweaks = tweaks; _backups = backups; _startup = startup; _network = network; _log = log;
    }

    public RevertAllResult RevertAll()
    {
        var failures = new List<string>();
        var count = 0;

        // Newest first, so overlapping changes unwind in reverse order.
        foreach (var backup in _backups.GetAll().OrderByDescending(b => b.CreatedUtc))
        {
            try
            {
                var tweak = _tweaks.FirstOrDefault(t => t.Id == backup.Id);
                if (tweak is not null) tweak.Revert();
                else if (backup.Id.StartsWith(StartupService.BackupPrefix)) _startup.Restore(backup);
                else if (backup.Id == NetworkService.DnsBackupId) _network.RestoreOriginalDns();
                else { failures.Add($"{backup.DisplayName}: unknown backup type, left untouched."); continue; }
                count++;
            }
            catch (Exception ex)
            {
                failures.Add($"{backup.DisplayName}: {ex.Message}");
            }
        }

        _log.Add("backup", $"Revert all: {count} change(s) reverted, {failures.Count} failed", ChangeAction.Reverted, failures);
        return new RevertAllResult(count, failures);
    }
}
