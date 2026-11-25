using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCOpti.Models;
using PCOpti.Services;

namespace PCOpti.ViewModels;

public partial class CleanItemViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IChangeLog _log;

    public CleanItemViewModel(CleanTarget target, IDialogService dialogs, IChangeLog log)
    {
        Target = target;
        _dialogs = dialogs;
        _log = log;
    }

    public CleanTarget Target { get; }
    public string Name => Target.Name;
    public string Description => Target.Description;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isScanned;
    [ObservableProperty] private long _bytes;
    [ObservableProperty] private string _sizeText = "Not scanned";
    [ObservableProperty] private string? _resultMessage;
    [ObservableProperty] private bool _resultIsError;

    [RelayCommand]
    public async Task ScanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ResultMessage = null;
        try
        {
            var r = await Task.Run(Target.Scan);
            Bytes = r.Bytes;
            SizeText = r.Files == 0 ? "Nothing to clean" : $"{Format.Bytes(r.Bytes)} in {r.Files:N0} file(s)";
            IsScanned = true;
        }
        catch (Exception ex)
        {
            SizeText = "Scan failed";
            ResultMessage = ex.Message; ResultIsError = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CleanAsync()
    {
        if (IsBusy) return;
        // Always show the real size before deleting anything.
        if (!IsScanned) await ScanAsync();
        if (Bytes == 0 && Target is not RecycleBinTarget)
        {
            ResultMessage = "Nothing to clean."; ResultIsError = false;
            return;
        }

        if (!_dialogs.Confirm($"Clean {Name}?",
                $"This will permanently delete about {Format.Bytes(Bytes)}.\n\n{Description}\n\nDeleted files can't be restored by PCOpti.",
                confirmText: "Delete", danger: true))
            return;

        IsBusy = true;
        try
        {
            var r = await Task.Run(Target.Clean);
            ResultIsError = r.Message is not null && r.FilesDeleted == 0;
            ResultMessage = r.Message ?? $"Freed {Format.Bytes(r.BytesFreed)} ({r.FilesDeleted:N0} files)." +
                            (r.FilesSkipped > 0 ? $" {r.FilesSkipped:N0} file(s) were in use and were skipped." : "");
            _log.Add("cleaner", $"Cleaned {Name}", ChangeAction.Cleaned, new[] { ResultMessage });
        }
        catch (Exception ex)
        {
            ResultMessage = ex.Message; ResultIsError = true;
        }
        finally
        {
            IsBusy = false;
            IsScanned = false;
        }

        // Re-scan so the size shown is what's really left, but keep the result message visible.
        var (message, isError) = (ResultMessage, ResultIsError);
        await ScanAsync();
        (ResultMessage, ResultIsError) = (message, isError);
    }
}

public partial class CleanerViewModel : PageViewModel
{
    public CleanerViewModel(IEnumerable<CleanTarget> targets, IDialogService dialogs, IChangeLog log)
    {
        Items = new ObservableCollection<CleanItemViewModel>(targets.Select(t => new CleanItemViewModel(t, dialogs, log)));
    }

    public override string Title => "Cleaner";
    public override string Icon => "";
    public ObservableCollection<CleanItemViewModel> Items { get; }

    [ObservableProperty] private string _totalText = "";

    protected override Task LoadAsync() => ScanAllAsync();

    [RelayCommand]
    private async Task ScanAllAsync()
    {
        IsBusy = true;
        await Task.WhenAll(Items.Select(i => i.ScanAsync()));
        TotalText = $"Total that could be freed: {Format.Bytes(Items.Sum(i => i.Bytes))}";
        IsBusy = false;
    }
}
