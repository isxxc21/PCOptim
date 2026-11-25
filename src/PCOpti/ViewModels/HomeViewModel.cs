using System.Security.Principal;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCOpti.Services;

namespace PCOpti.ViewModels;

/// <summary>Home: live usage and a short guide. Deliberately has no "optimise everything" button.</summary>
public partial class HomeViewModel : PageViewModel
{
    private readonly SystemStatsService _stats;
    private readonly IBackupStore _backups;

    public HomeViewModel(SystemStatsService stats, IBackupStore backups)
    {
        _stats = stats;
        _backups = backups;
        _stats.Updated += (_, s) => Application.Current?.Dispatcher.BeginInvoke(() => Apply(s));
        _backups.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(UpdateActiveChanges);
        UpdateActiveChanges();
        IsAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public override string Title => "Home";
    public override string Icon => "";

    public bool IsAdmin { get; }

    [ObservableProperty] private double _cpu;
    [ObservableProperty] private double _ram;
    [ObservableProperty] private double _disk;
    [ObservableProperty] private double _gpu;
    [ObservableProperty] private bool _gpuAvailable;
    [ObservableProperty] private string _cpuText = "...";
    [ObservableProperty] private string _ramText = "...";
    [ObservableProperty] private string _ramDetail = "";
    [ObservableProperty] private string _diskText = "...";
    [ObservableProperty] private string _gpuText = "...";
    [ObservableProperty] private int _activeChanges;

    private void Apply(SystemStats s)
    {
        Cpu = s.CpuPercent; CpuText = $"{s.CpuPercent:0}%";
        Ram = s.RamPercent; RamText = $"{s.RamPercent:0}%"; RamDetail = $"{s.RamUsedGb:0.0} / {s.RamTotalGb:0.0} GB";
        Disk = s.DiskPercent; DiskText = $"{s.DiskPercent:0}%";
        GpuAvailable = s.GpuPercent.HasValue;
        if (s.GpuPercent is { } g) { Gpu = g; GpuText = $"{g:0}%"; }
        else if (GpuText == "...") GpuText = "n/a";
    }

    private void UpdateActiveChanges() => ActiveChanges = _backups.GetAll().Count;
}
