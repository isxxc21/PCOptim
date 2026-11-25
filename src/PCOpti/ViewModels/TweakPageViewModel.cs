using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace PCOpti.ViewModels;

/// <summary>A page generated from the tweak catalog (Gaming Tweaks, Services).</summary>
public partial class TweakPageViewModel : PageViewModel
{
    public TweakPageViewModel(string title, string icon, string subtitle, string? footnote, IEnumerable<TweakCardViewModel> cards)
    {
        Title = title;
        Icon = icon;
        Subtitle = subtitle;
        Footnote = footnote;
        Cards = new ObservableCollection<TweakCardViewModel>(cards);
    }

    public override string Title { get; }
    public override string Icon { get; }
    public string Subtitle { get; }
    public string? Footnote { get; }
    public ObservableCollection<TweakCardViewModel> Cards { get; }

    protected override Task LoadAsync() => RefreshAsync();
    protected override Task OnReactivatedAsync() => RefreshAsync();

    /// <summary>Re-reads every card's state (each on a background thread, in parallel).</summary>
    [RelayCommand]
    private Task RefreshAsync() => Task.WhenAll(Cards.Select(c => c.RefreshAsync()));
}
