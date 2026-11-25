using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCOpti.Models;
using PCOpti.Services;

namespace PCOpti.ViewModels;

public partial class StartupItemViewModel : ObservableObject
{
    private readonly StartupService _service;
    private readonly SafetyGate _gate;

    public StartupItemViewModel(StartupEntry entry, StartupService service, SafetyGate gate)
    {
        Entry = entry;
        _service = service;
        _gate = gate;
        _isEnabled = entry.Enabled;
    }

    public StartupEntry Entry { get; }
    public string Name => Entry.Name;
    public string Publisher => string.IsNullOrWhiteSpace(Entry.Publisher) ? "Unknown publisher" : Entry.Publisher;
    public string Command => Entry.Command;
    public string SourceText => Entry.SourceText;
    public string ImpactText => Entry.Impact == StartupImpact.Unknown ? "Unknown" : Entry.Impact.ToString();
    public StartupImpact Impact => Entry.Impact;

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (IsBusy) return;
        var target = !Entry.Enabled;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (!target) await _gate.OfferRestorePointAsync();
            await Task.Run(() => _service.SetEnabled(Entry, target));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex is UnauthorizedAccessException
                ? "Access denied. This entry may be protected by policy or security software."
                : ex.Message;
        }
        finally
        {
            IsEnabled = Entry.Enabled;
            OnPropertyChanged(nameof(IsEnabled)); // re-sync the switch if the change failed
            IsBusy = false;
        }
    }
}

public partial class StartupViewModel : PageViewModel
{
    private readonly StartupService _service;
    private readonly SafetyGate _gate;

    public StartupViewModel(StartupService service, SafetyGate gate)
    {
        _service = service;
        _gate = gate;
    }

    public override string Title => "Startup Apps";
    public override string Icon => "";

    public ObservableCollection<StartupItemViewModel> Items { get; } = new();

    [ObservableProperty] private string _summary = "";

    protected override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        SetStatus(null);
        try
        {
            var entries = await Task.Run(_service.GetEntries);
            Items.Clear();
            foreach (var e in entries) Items.Add(new StartupItemViewModel(e, _service, _gate));
            var enabled = entries.Count(e => e.Enabled);
            Summary = $"{entries.Count} startup items, {enabled} enabled.";
        }
        catch (Exception ex)
        {
            SetStatus($"Couldn't read startup items: {ex.Message}", true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
