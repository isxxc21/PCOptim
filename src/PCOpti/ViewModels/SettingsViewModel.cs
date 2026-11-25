using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCOpti.Services;

namespace PCOpti.ViewModels;

public partial class SettingsViewModel : PageViewModel
{
    private readonly SettingsService _settings;
    private readonly UpdateChecker _updates;

    public SettingsViewModel(SettingsService settings, UpdateChecker updates)
    {
        _settings = settings;
        _updates = updates;
        _selectedAccent = ThemeService.Accents.FirstOrDefault(a => a.Name == settings.Current.Accent) ?? ThemeService.Accents[0];
        _checkOnStartup = settings.Current.CheckForUpdatesOnStartup;
    }

    public override string Title => "Settings";
    public override string Icon => "";

    public IReadOnlyList<AccentOption> Accents => ThemeService.Accents;
    public string VersionText => AppInfo.VersionText;
    public string RepoUrl => AppInfo.RepoUrl;

    [ObservableProperty] private AccentOption _selectedAccent;
    [ObservableProperty] private bool _checkOnStartup;
    [ObservableProperty] private string? _updateMessage;
    [ObservableProperty] private string? _updateUrl;

    partial void OnSelectedAccentChanged(AccentOption value)
    {
        ThemeService.Apply(value.Name);
        _settings.Current.Accent = value.Name;
        _settings.Save();
    }

    partial void OnCheckOnStartupChanged(bool value)
    {
        _settings.Current.CheckForUpdatesOnStartup = value;
        _settings.Save();
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        IsBusy = true;
        UpdateUrl = null;
        try
        {
            var info = await _updates.CheckAsync();
            if (info.IsNewer)
            {
                UpdateMessage = $"Version {info.TagName} is available. Download it from the releases page; PCOpti never updates itself.";
                UpdateUrl = info.Url;
            }
            else
            {
                UpdateMessage = $"You're up to date ({AppInfo.VersionText}).";
            }
        }
        catch (Exception ex)
        {
            UpdateMessage = $"Couldn't check for updates: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void OpenRepo() => OpenUrl(AppInfo.RepoUrl);

    [RelayCommand]
    private void OpenUpdate() => OpenUrl(UpdateUrl ?? AppInfo.ReleasesUrl);

    private static void OpenUrl(string url)
    {
        // Open through Explorer so the browser doesn't inherit PCOpti's admin rights.
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{url}\"") { UseShellExecute = false })?.Dispose();
    }
}
