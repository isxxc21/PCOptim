using PCOpti.Models;

namespace PCOpti.Services;

/// <summary>
/// Runs before any change: asks for confirmation on Moderate/Advanced items, and once per session offers to
/// create a System Restore point. Every page goes through this so the safety rules live in one place.
/// </summary>
public sealed class SafetyGate
{
    private readonly IDialogService _dialogs;
    private readonly RestorePointService _restore;
    private readonly SettingsService _settings;
    private bool _offeredThisSession;

    public SafetyGate(IDialogService dialogs, RestorePointService restore, SettingsService settings)
    {
        _dialogs = dialogs; _restore = restore; _settings = settings;
    }

    /// <summary>Returns false if the user cancelled. Call on the UI thread.</summary>
    public bool ConfirmRisk(string name, RiskLevel risk, string description, string? warning)
    {
        if (risk == RiskLevel.Safe) return true;
        var label = risk == RiskLevel.Advanced ? "Advanced" : "Moderate";
        return _dialogs.Confirm(
            $"Apply \"{name}\"?",
            $"This change is rated {label}.\n\n{description}\n\nPCOpti saves the current setting first, so you can revert it at any time.",
            confirmText: "Apply",
            danger: risk == RiskLevel.Advanced,
            warning: warning ?? "Make sure you understand what this changes before continuing.");
    }

    /// <summary>Offers a restore point before the first change of the session (if enabled in settings).</summary>
    public async Task OfferRestorePointAsync()
    {
        if (_offeredThisSession || !_settings.Current.AskForRestorePoint) return;
        _offeredThisSession = true;

        var choice = _dialogs.Choose(
            "Create a System Restore point first?",
            "PCOpti always saves the original value of everything it changes, so each change can be reverted from PCOpti. " +
            "A System Restore point is an extra safety net that also works if PCOpti itself can't run.\n\n" +
            "Creating one usually takes 10 to 60 seconds.",
            "Create restore point", "Skip", "Skip and don't ask again");

        if (choice == 2)
        {
            _settings.Current.AskForRestorePoint = false;
            _settings.Save();
        }
        if (choice != 0) return;

        var result = await Task.Run(() => _restore.Create("PCOpti - before changes"));
        _dialogs.Info(result.Success ? "Restore point created" : "Restore point not created", result.Message, isError: !result.Success);
    }
}
