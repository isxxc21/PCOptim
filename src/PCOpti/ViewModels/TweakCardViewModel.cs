using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCOpti.Models;
using PCOpti.Services;
using PCOpti.Tweaks;

namespace PCOpti.ViewModels;

/// <summary>One tweak card: name, description, risk badge, live status and its own Apply / Revert.</summary>
public partial class TweakCardViewModel : ObservableObject
{
    private readonly SafetyGate _gate;

    public TweakCardViewModel(ITweak tweak, SafetyGate gate, bool useToggle)
    {
        Tweak = tweak;
        _gate = gate;
        UseToggle = useToggle;
    }

    public ITweak Tweak { get; }
    public string Name => Tweak.Name;
    public string Description => Tweak.Description;
    public string? Note => Tweak.Note;
    public RiskLevel Risk => Tweak.RiskLevel;
    public string RiskText => Tweak.RiskLevel.ToString();
    /// <summary>Services page shows a switch; Gaming page shows Apply / Revert buttons.</summary>
    public bool UseToggle { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isApplied;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isLoaded;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAvailable = true;
    [ObservableProperty] private string? _unavailableReason;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private bool _hasBackup;

    public string StatusText => !IsLoaded ? "Checking..." : IsApplied ? "Applied" : "Not applied";

    /// <summary>Reads the real state from the system on a background thread.</summary>
    public async Task RefreshAsync()
    {
        try
        {
            var (reason, applied, backup) = await Task.Run(() =>
            {
                var r = Tweak.GetUnavailableReason();
                return (r, r is null && Tweak.IsApplied(), Tweak.HasBackup);
            });
            UnavailableReason = reason;
            IsAvailable = reason is null;
            IsApplied = applied;
            HasBackup = backup;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't read the current state: {TweakException.From(ex).Message}";
        }
        finally
        {
            IsLoaded = true;
            OnPropertyChanged(nameof(IsApplied)); // re-sync toggle switches even if the value didn't change
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (IsBusy || !IsAvailable) return;
        if (!_gate.ConfirmRisk(Name, Risk, Description, Tweak.Warning)) { OnPropertyChanged(nameof(IsApplied)); return; }
        await RunAsync(apply: true);
    }

    [RelayCommand]
    private async Task RevertAsync()
    {
        if (IsBusy) return;
        await RunAsync(apply: false);
    }

    /// <summary>Used by toggle switches: apply when off, revert when on.</summary>
    [RelayCommand]
    private Task ToggleAsync() => IsApplied ? RevertAsync() : ApplyAsync();

    private async Task RunAsync(bool apply)
    {
        IsBusy = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            if (apply) await _gate.OfferRestorePointAsync();
            await Task.Run(apply ? Tweak.Apply : Tweak.Revert);
            SuccessMessage = apply
                ? (Note is { Length: > 0 } n && n.Contains("restart", StringComparison.OrdinalIgnoreCase) ? "Applied. Restart your PC to finish." : "Applied.")
                : "Reverted to the previous setting.";
        }
        catch (Exception ex)
        {
            // Show the problem on this card only; the rest of the app keeps working.
            ErrorMessage = TweakException.From(ex).Message;
        }
        finally
        {
            await RefreshAsync();
            IsBusy = false;
        }
    }
}
