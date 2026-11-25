using System.Text.Json;
using PCOpti.Models;

namespace PCOpti.Services;

public sealed record NetworkAdapterInfo(int Index, string Name, string Description, string Guid, string CurrentDns, bool IsAutomatic);

public sealed record DnsProvider(string Name, string[] Servers)
{
    public static readonly DnsProvider Cloudflare = new("Cloudflare",
        new[] { "1.1.1.1", "1.0.0.1", "2606:4700:4700::1111", "2606:4700:4700::1001" });
    public static readonly DnsProvider Google = new("Google",
        new[] { "8.8.8.8", "8.8.4.4", "2001:4860:4860::8888", "2001:4860:4860::8844" });
}

/// <summary>
/// DNS flush, Winsock reset and DNS switching. Before DNS is first changed, the original setting of every
/// adapter is saved so "Restore original DNS" (and Revert All) can put it back exactly.
/// </summary>
public sealed class NetworkService
{
    public const string DnsBackupId = "network.dns";
    private const string TcpipInterfaces = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
    private const string Tcpip6Interfaces = @"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters\Interfaces";

    private readonly ICommandRunner _cmd;
    private readonly IRegistryAccess _reg;
    private readonly IBackupStore _backups;
    private readonly IChangeLog _log;

    public NetworkService(ICommandRunner cmd, IRegistryAccess reg, IBackupStore backups, IChangeLog log)
    {
        _cmd = cmd; _reg = reg; _backups = backups; _log = log;
    }

    public bool HasDnsBackup => _backups.Get(DnsBackupId) is not null;

    public string FlushDns()
    {
        var r = _cmd.Run(Path.Combine(Environment.SystemDirectory, "ipconfig.exe"), "/flushdns");
        if (!r.Success) throw new InvalidOperationException($"ipconfig failed: {r.ErrorText}");
        _log.Add("network", "Flushed DNS cache", ChangeAction.Ran, new[] { "ipconfig /flushdns" });
        return "DNS cache cleared. Websites and game servers are looked up fresh next time.";
    }

    public string ResetWinsock()
    {
        var r = _cmd.Run(Path.Combine(Environment.SystemDirectory, "netsh.exe"), "winsock reset");
        if (!r.Success) throw new InvalidOperationException($"netsh failed: {r.ErrorText}");
        _log.Add("network", "Reset Winsock catalog", ChangeAction.Ran,
            new[] { "netsh winsock reset", "This resets to Windows defaults; there is no previous state to restore." });
        return "Winsock was reset. Restart your PC to finish. If a VPN or security program stops working, reinstall or repair it.";
    }

    private sealed record AdapterDto(int Index, string? Name, string? Description, string? Guid, string? Dns);

    public List<NetworkAdapterInfo> GetAdapters()
    {
        const string script = """
            $items = Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | ForEach-Object {
                $dns = (Get-DnsClientServerAddress -InterfaceIndex $_.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).ServerAddresses
                [pscustomobject]@{ Index = [int]$_.ifIndex; Name = $_.Name; Description = $_.InterfaceDescription;
                                   Guid = [string]$_.InterfaceGuid; Dns = ($dns -join ', ') }
            }
            ConvertTo-Json -InputObject @($items) -Compress
            """;
        var r = _cmd.RunPowerShell(script, 60_000);
        if (!r.Success) throw new InvalidOperationException($"Couldn't list network adapters: {r.ErrorText}");

        var dtos = JsonSerializer.Deserialize<List<AdapterDto>>(r.StdOut.Trim()) ?? new();
        return dtos.Where(d => !string.IsNullOrEmpty(d.Guid)).Select(d =>
        {
            var guid = NormalizeGuid(d.Guid!);
            var isAuto = string.IsNullOrWhiteSpace(ReadNameServer(TcpipInterfaces, guid));
            return new NetworkAdapterInfo(d.Index, d.Name ?? "", d.Description ?? "", guid, d.Dns ?? "", isAuto);
        }).ToList();
    }

    private static string NormalizeGuid(string g) => "{" + g.Trim('{', '}').ToUpperInvariant() + "}";

    private string ReadNameServer(string root, string guid) =>
        _reg.GetValue(RegHive.LocalMachine, $@"{root}\{guid}", "NameServer")?.Data ?? "";

    /// <summary>Points every connected physical adapter at the given DNS provider.</summary>
    public string SetDns(DnsProvider provider)
    {
        var adapters = GetAdapters();
        if (adapters.Count == 0) throw new InvalidOperationException("No connected network adapters were found.");
        BackupDns(adapters);

        var list = string.Join(",", provider.Servers.Select(s => $"'{s}'"));
        foreach (var a in adapters)
            RunOrThrow($"Set-DnsClientServerAddress -InterfaceIndex {a.Index} -ServerAddresses @({list})");
        RunOrThrow("Clear-DnsClientCache");

        _log.Add("network", $"DNS set to {provider.Name}", ChangeAction.Applied,
            adapters.Select(a => $"{a.Name}: {(a.IsAutomatic ? "Automatic" : a.CurrentDns)} -> {string.Join(", ", provider.Servers)}"));
        return $"DNS set to {provider.Name} on {adapters.Count} adapter(s).";
    }

    /// <summary>Lets each adapter get DNS from the router again (DHCP).</summary>
    public string SetAutomatic()
    {
        var adapters = GetAdapters();
        BackupDns(adapters);
        foreach (var a in adapters)
            RunOrThrow($"Set-DnsClientServerAddress -InterfaceIndex {a.Index} -ResetServerAddresses");
        RunOrThrow("Clear-DnsClientCache");
        _log.Add("network", "DNS set to Automatic (from your router)", ChangeAction.Applied,
            adapters.Select(a => $"{a.Name}: {(a.IsAutomatic ? "Automatic" : a.CurrentDns)} -> Automatic"));
        return $"DNS set to Automatic on {adapters.Count} adapter(s).";
    }

    /// <summary>Puts DNS back exactly as it was before PCOpti first changed it.</summary>
    public string RestoreOriginalDns()
    {
        var backup = _backups.Get(DnsBackupId);
        if (backup is null) return "There is no saved DNS setting to restore.";

        var adapters = GetAdapters().ToDictionary(a => a.Guid, StringComparer.OrdinalIgnoreCase);
        var details = new List<string>();
        foreach (var (guid, v4) in backup.State.Where(kv => !kv.Key.EndsWith(".v6")))
        {
            if (!adapters.TryGetValue(guid, out var a))
            {
                details.Add($"{guid}: adapter not connected, skipped");
                continue;
            }
            var v6 = backup.State.GetValueOrDefault(guid + ".v6") ?? "";
            var servers = SplitServers(v4).Concat(SplitServers(v6)).ToList();

            RunOrThrow($"Set-DnsClientServerAddress -InterfaceIndex {a.Index} -ResetServerAddresses");
            if (servers.Count > 0)
                RunOrThrow($"Set-DnsClientServerAddress -InterfaceIndex {a.Index} -ServerAddresses @({string.Join(",", servers.Select(s => $"'{s}'"))})");
            details.Add($"{a.Name}: restored {(servers.Count == 0 ? "Automatic" : string.Join(", ", servers))}");
        }
        RunOrThrow("Clear-DnsClientCache");
        _backups.Remove(DnsBackupId);
        _log.Add("network", "Restored original DNS settings", ChangeAction.Reverted, details);
        return "Original DNS settings restored.";
    }

    private void BackupDns(IEnumerable<NetworkAdapterInfo> adapters)
    {
        // Keep the very first original values; add adapters we haven't seen before.
        var existing = _backups.Get(DnsBackupId);
        var state = existing is null ? new Dictionary<string, string>() : new Dictionary<string, string>(existing.State);
        var changed = existing is null;
        foreach (var a in adapters)
        {
            if (state.ContainsKey(a.Guid)) continue;
            state[a.Guid] = ReadNameServer(TcpipInterfaces, a.Guid);
            state[a.Guid + ".v6"] = ReadNameServer(Tcpip6Interfaces, a.Guid);
            changed = true;
        }
        if (!changed) return;
        if (existing is not null) _backups.Remove(DnsBackupId);
        _backups.TryAdd(new TweakBackup { Id = DnsBackupId, DisplayName = "DNS servers", CreatedUtc = existing?.CreatedUtc ?? DateTime.UtcNow, State = state });
    }

    private static IEnumerable<string> SplitServers(string s) =>
        s.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .Where(x => System.Net.IPAddress.TryParse(x, out _)); // only ever pass real IPs to PowerShell

    private void RunOrThrow(string script)
    {
        var r = _cmd.RunPowerShell(script, 60_000);
        if (!r.Success) throw new InvalidOperationException(r.ErrorText.Split('\n').FirstOrDefault()?.Trim() ?? "PowerShell command failed.");
    }
}
