using PCOpti.Services;

namespace PCOpti.Tweaks;

/// <summary>Everything a tweak may touch, passed in so tests can supply fakes.</summary>
public sealed record TweakContext(
    IRegistryAccess Registry,
    IBackupStore Backups,
    IChangeLog Log,
    ICommandRunner Commands,
    ISystemNotifier Notifier);
