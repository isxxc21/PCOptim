using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCOpti.Models;
using PCOpti.Services;

namespace PCOpti.ViewModels;

public sealed record BackupRow(string Name, string When, string Details);

public partial class BackupViewModel : PageViewModel
{
    private readonly IBackupStore _backups;
    private readonly IChangeLog _log;
    private readonly RevertAllService _revertAll;
    private readonly RestorePointService _restore;
    private readonly SettingsService _settings;
    private readonly IDialogService _dialogs;

    /// <summary>Raised after Revert All so the other pages re-read their state.</summary>
    public event EventHandler? RevertedAll;

    public BackupViewModel(IBackupStore backups, IChangeLog log, RevertAllService revertAll, RestorePointService restore,
        SettingsService settings, IDialogService dialogs)
    {
        _backups = backups; _log = log; _revertAll = revertAll; _restore = restore; _settings = settings; _dialogs = dialogs;
        _askForRestorePoint = settings.Current.AskForRestorePoint;
        _backups.Changed += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(Reload);
        _log.Changed += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(Reload);
    }

    public override string Title => "Backup & Restore";
    public override string Icon => "";

    public ObservableCollection<BackupRow> Backups { get; } = new();
    public ObservableCollection<ChangeEntry> Log { get; } = new();

    [ObservableProperty] private bool _askForRestorePoint;

    partial void OnAskForRestorePointChanged(bool value)
    {
        _settings.Current.AskForRestorePoint = value;
        _settings.Save();
    }

    protected override Task LoadAsync() { Reload(); return Task.CompletedTask; }
    protected override Task OnReactivatedAsync() { Reload(); return Task.CompletedTask; }

    private void Reload()
    {
        Backups.Clear();
        foreach (var b in _backups.GetAll().OrderByDescending(b => b.CreatedUtc))
        {
            var details = b.Registry.Count > 0
                ? string.Join("\n", b.Registry.Select(r => $"{r.Location} = {(r.Existed ? r.Value?.ToString() : "(not set)")}"))
                : string.Join("\n", b.State.Select(kv => $"{kv.Key} = {(string.IsNullOrEmpty(kv.Value) ? "(automatic / empty)" : kv.Value)}"));
            Backups.Add(new BackupRow(b.DisplayName, b.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), details));
        }
        Log.Clear();
        foreach (var e in _log.GetAll().Take(500)) Log.Add(e);
    }

    [RelayCommand]
    private async Task RevertAllAsync()
    {
        var count = _backups.GetAll().Count;
        if (count == 0) { SetStatus("There are no PCOpti changes to revert."); return; }
        if (!_dialogs.Confirm("Revert all PCOpti changes?",
                $"PCOpti will restore the original settings it saved for {count} change(s): tweaks, services, startup items and DNS.\n\n" +
                "Files deleted by the Cleaner and Winsock resets can't be undone.",
                confirmText: "Revert all", danger: true))
            return;

        IsBusy = true;
        try
        {
            var r = await Task.Run(_revertAll.RevertAll);
            SetStatus(r.Failures.Count == 0
                ? $"Reverted {r.Reverted} change(s). Some settings need a restart to fully go back."
                : $"Reverted {r.Reverted}, but {r.Failures.Count} failed:\n{string.Join("\n", r.Failures)}", r.Failures.Count > 0);
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
        finally { IsBusy = false; }
        Reload();
        RevertedAll?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        IsBusy = true;
        SetStatus("Creating a restore point. This can take up to a minute...");
        var r = await Task.Run(() => _restore.Create("PCOpti - manual restore point"));
        SetStatus(r.Message, !r.Success);
        IsBusy = false;
    }

    [RelayCommand]
    private void OpenDataFolder() =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppInfo.DataFolder}\"") { UseShellExecute = false })?.Dispose();

    [RelayCommand]
    private void ClearLog()
    {
        if (_dialogs.Confirm("Clear the change log?", "This only clears the history list. Saved backups are kept, so Revert still works.", "Clear log"))
            _log.Clear();
    }
}
