using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PCOpti.ViewModels;

/// <summary>Base for every sidebar page.</summary>
public abstract partial class PageViewModel : ObservableObject
{
    public abstract string Title { get; }
    /// <summary>Segoe Fluent / MDL2 glyph for the sidebar.</summary>
    public abstract string Icon { get; }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _statusIsError;

    private bool _loadedOnce;

    /// <summary>Called each time the page is shown. Loads data the first time.</summary>
    public async Task ActivateAsync()
    {
        if (_loadedOnce) { await OnReactivatedAsync(); return; }
        _loadedOnce = true;
        await LoadAsync();
    }

    protected virtual Task LoadAsync() => Task.CompletedTask;
    protected virtual Task OnReactivatedAsync() => Task.CompletedTask;

    protected void SetStatus(string? message, bool isError = false)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }
}

public static class Format
{
    public static string Bytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        var i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return v.ToString(i == 0 ? "0" : v < 10 ? "0.0" : "0", CultureInfo.CurrentCulture) + " " + units[i];
    }
}
