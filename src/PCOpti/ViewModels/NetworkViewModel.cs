using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCOpti.Models;
using PCOpti.Services;

namespace PCOpti.ViewModels;

public partial class NetworkViewModel : PageViewModel
{
    private readonly NetworkService _network;
    private readonly SafetyGate _gate;
    private readonly IDialogService _dialogs;

    public NetworkViewModel(NetworkService network, SafetyGate gate, IDialogService dialogs)
    {
        _network = network;
        _gate = gate;
        _dialogs = dialogs;
    }

    public override string Title => "Network";
    public override string Icon => "";

    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = new();

    [ObservableProperty] private bool _hasDnsBackup;

    protected override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var list = await Task.Run(_network.GetAdapters);
            Adapters.Clear();
            foreach (var a in list) Adapters.Add(a);
            HasDnsBackup = _network.HasDnsBackup;
            if (list.Count == 0) SetStatus("No connected network adapters found.", true);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task FlushDnsAsync() => RunAsync(_network.FlushDns, refresh: false);

    [RelayCommand]
    private Task ResetWinsockAsync()
    {
        if (!_gate.ConfirmRisk("Reset Winsock", RiskLevel.Advanced,
                "Resets Windows' network socket settings to their defaults. This can fix \"connected but no internet\" " +
                "problems caused by broken network software. It does not make a working connection faster.",
                "This can't be undone by PCOpti (it resets to Windows defaults). VPNs, proxies or security software that " +
                "hook into networking may need reinstalling. You must restart your PC afterwards."))
            return Task.CompletedTask;
        return RunAsync(_network.ResetWinsock, refresh: false);
    }

    [RelayCommand]
    private Task UseCloudflareAsync() => SetDnsAsync(DnsProvider.Cloudflare);

    [RelayCommand]
    private Task UseGoogleAsync() => SetDnsAsync(DnsProvider.Google);

    [RelayCommand]
    private async Task UseAutomaticAsync()
    {
        await _gate.OfferRestorePointAsync();
        await RunAsync(_network.SetAutomatic, refresh: true);
    }

    [RelayCommand]
    private Task RestoreOriginalAsync() => RunAsync(_network.RestoreOriginalDns, refresh: true);

    private async Task SetDnsAsync(DnsProvider provider)
    {
        if (!_dialogs.Confirm($"Switch DNS to {provider.Name}?",
                $"All connected adapters will use {provider.Name} DNS ({string.Join(", ", provider.Servers.Take(2))}).\n\n" +
                "Your current DNS settings are saved first, so \"Restore original DNS\" puts them back exactly.",
                confirmText: "Switch DNS"))
            return;
        await _gate.OfferRestorePointAsync();
        await RunAsync(() => _network.SetDns(provider), refresh: true);
    }

    private async Task RunAsync(Func<string> action, bool refresh)
    {
        IsBusy = true;
        SetStatus(null);
        try
        {
            SetStatus(await Task.Run(action));
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, true);
        }
        finally { IsBusy = false; }
        if (refresh)
        {
            var msg = (StatusMessage, StatusIsError);
            await RefreshAsync();
            if (!StatusIsError) SetStatus(msg.StatusMessage, msg.StatusIsError);
        }
    }
}
