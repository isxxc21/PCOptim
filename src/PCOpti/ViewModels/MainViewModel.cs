using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PCOpti.ViewModels;

/// <summary>Owns the sidebar and the page currently shown.</summary>
public partial class MainViewModel : ObservableObject
{
    public MainViewModel(IEnumerable<PageViewModel> pages)
    {
        Pages = new ObservableCollection<PageViewModel>(pages);
    }

    public ObservableCollection<PageViewModel> Pages { get; }
    public string VersionText => AppInfo.VersionText;

    [ObservableProperty] private PageViewModel? _currentPage;

    /// <summary>Shown in the sidebar when the optional startup update check finds a newer release.</summary>
    [ObservableProperty] private string? _updateNotice;

    [RelayCommand]
    private void OpenSettings() => CurrentPage = Pages.LastOrDefault();

    partial void OnCurrentPageChanged(PageViewModel? value)
    {
        if (value is not null) _ = SafeActivate(value);
    }

    private static async Task SafeActivate(PageViewModel page)
    {
        try { await page.ActivateAsync(); }
        catch (Exception ex) { page.StatusMessage = ex.Message; page.StatusIsError = true; }
    }

    [RelayCommand]
    private void Navigate(PageViewModel page) => CurrentPage = page;
}
