using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCOpti.Services;

namespace PCOpti.ViewModels;

public partial class BoostAppViewModel : ObservableObject
{
    public BoostAppViewModel(RunningApp app) => App = app;

    public RunningApp App { get; }
    public string Name => string.IsNullOrWhiteSpace(App.Title) ? App.ProcessName : $"{App.ProcessName}  ·  {App.Title}";
    public string Detail => $"{Format.Bytes(App.MemoryBytes)} RAM{(App.ProcessCount > 1 ? $" · {App.ProcessCount} processes" : "")}{(App.HasWindow ? "" : " · background / tray")}";
    public string Path => App.Path;

    [ObservableProperty] private bool _isSelected;
}

public partial class GameBoostViewModel : PageViewModel
{
    private readonly GameBoostService _boost;
    private readonly IDialogService _dialogs;

    public GameBoostViewModel(GameBoostService boost, IDialogService dialogs)
    {
        _boost = boost;
        _dialogs = dialogs;
    }

    public override string Title => "Game Boost";
    public override string Icon => "";

    /// <summary>Apps that can be ticked and closed.</summary>
    public ObservableCollection<BoostAppViewModel> Apps { get; } = new();
    /// <summary>Windowed apps, one of which can be picked as the game for priority boost.</summary>
    public ObservableCollection<BoostAppViewModel> Games { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RaisePriorityCommand), nameof(RestorePriorityCommand))]
    [NotifyPropertyChangedFor(nameof(SelectedIsBoosted))]
    private BoostAppViewModel? _selectedGame;

    [ObservableProperty] private int _closedCount;
    [ObservableProperty] private string? _priorityMessage;

    public bool SelectedIsBoosted => SelectedGame is not null && _boost.IsBoosted(SelectedGame.App.MainPid);

    protected override Task LoadAsync() => RefreshAsync();
    protected override Task OnReactivatedAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var apps = await Task.Run(_boost.GetRunningApps);
            var selected = Apps.Where(a => a.IsSelected).Select(a => a.App.ProcessName).ToHashSet();
            var game = SelectedGame?.App.ProcessName;
            Apps.Clear();
            Games.Clear();
            foreach (var a in apps)
            {
                Apps.Add(new BoostAppViewModel(a) { IsSelected = selected.Contains(a.ProcessName) });
                if (a.HasWindow) Games.Add(new BoostAppViewModel(a));
            }
            SelectedGame = Games.FirstOrDefault(a => a.App.ProcessName == game);
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CloseSelectedAsync()
    {
        var chosen = Apps.Where(a => a.IsSelected).ToList();
        if (chosen.Count == 0) { SetStatus("Tick the apps you want to close first."); return; }

        if (!_dialogs.Confirm("Close selected apps?",
                "PCOpti will ask these apps to close normally, like clicking their X button, so they can save your work:\n\n" +
                string.Join("\n", chosen.Select(c => "  •  " + c.App.ProcessName)) +
                "\n\nYou can reopen them afterwards with \"Reopen closed apps\".",
                confirmText: "Close apps"))
            return;

        IsBusy = true;
        try
        {
            var stillRunning = await Task.Run(() => _boost.CloseApps(chosen.Select(c => c.App), force: false));
            if (stillRunning.Count > 0 && _dialogs.Confirm("Some apps are still running",
                    "These apps didn't close. They may only live in the system tray or be asking to save:\n\n" +
                    string.Join("\n", stillRunning.Select(s => "  •  " + s)) +
                    "\n\nForce-closing ends them immediately, and any unsaved work in them is lost.",
                    confirmText: "Force close", danger: true))
            {
                stillRunning = await Task.Run(() => _boost.CloseApps(chosen.Where(c => stillRunning.Contains(c.App.ProcessName)).Select(c => c.App), force: true));
            }
            SetStatus(stillRunning.Count == 0 ? "Selected apps closed." : $"Still running: {string.Join(", ", stillRunning)}", stillRunning.Count > 0);
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
        finally
        {
            IsBusy = false;
            ClosedCount = _boost.ClosedApps.Count;
        }
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ReopenClosedAsync()
    {
        var n = await Task.Run(_boost.ReopenClosedApps);
        ClosedCount = 0;
        SetStatus($"Reopened {n} app(s).");
        await Task.Delay(1500);
        await RefreshAsync();
    }

    private bool CanChangePriority() => SelectedGame is not null;

    [RelayCommand(CanExecute = nameof(CanChangePriority))]
    private void RaisePriority()
    {
        if (SelectedGame is null) return;
        try
        {
            _boost.RaisePriority(SelectedGame.App.MainPid);
            PriorityMessage = $"{SelectedGame.App.ProcessName} now runs at High priority until it closes.";
        }
        catch (Exception ex) { PriorityMessage = $"Couldn't change priority: {ex.Message}"; }
        OnPropertyChanged(nameof(SelectedIsBoosted));
    }

    [RelayCommand(CanExecute = nameof(CanChangePriority))]
    private void RestorePriority()
    {
        if (SelectedGame is null) return;
        try
        {
            _boost.RestorePriority(SelectedGame.App.MainPid);
            PriorityMessage = $"{SelectedGame.App.ProcessName} priority restored.";
        }
        catch (Exception ex) { PriorityMessage = $"Couldn't change priority: {ex.Message}"; }
        OnPropertyChanged(nameof(SelectedIsBoosted));
    }
}
