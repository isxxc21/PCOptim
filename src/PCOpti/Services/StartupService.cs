using System.Diagnostics;
using System.Text.Json;
using PCOpti.Models;

namespace PCOpti.Services;

/// <summary>
/// Lists and toggles startup programs. Disabling uses the same mechanism as Task Manager
/// (the StartupApproved registry flags) or Task Scheduler's own enable/disable, so nothing is ever deleted
/// and Task Manager shows the same state.
/// </summary>
public sealed class StartupService
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32Path = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private readonly IRegistryAccess _reg;
    private readonly ICommandRunner _cmd;
    private readonly IBackupStore _backups;
    private readonly IChangeLog _log;

    private readonly IReadOnlyList<(string Folder, StartupSource Source)> _folders;

    /// <param name="startupFolders">Override for tests; defaults to the real user and all-users Startup folders.</param>
    public StartupService(IRegistryAccess reg, ICommandRunner cmd, IBackupStore backups, IChangeLog log,
        IReadOnlyList<(string Folder, StartupSource Source)>? startupFolders = null)
    {
        _reg = reg; _cmd = cmd; _backups = backups; _log = log;
        _folders = startupFolders ?? new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.StartupFolderUser),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.StartupFolderAllUsers),
        };
    }

    public const string BackupPrefix = "startup:";

    // ---------------------------------------------------------------- StartupApproved flag format
    // 12-byte binary: first byte even (0x02, 0x06) = enabled, odd (0x03) = disabled, then a FILETIME.

    internal static bool IsApprovedEnabled(RegValue? value)
    {
        if (value is null || value.Kind != RegKind.Binary) return true; // no flag = enabled
        var bytes = value.AsBytes();
        return bytes.Length == 0 || (bytes[0] & 1) == 0;
    }

    internal static RegValue MakeApprovedValue(bool enabled, DateTime nowUtc)
    {
        var bytes = new byte[12];
        bytes[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled) BitConverter.GetBytes(nowUtc.ToFileTimeUtc()).CopyTo(bytes, 4);
        return new RegValue(RegKind.Binary, Convert.ToHexString(bytes));
    }

    private static (RegHive Hive, string Path)? ApprovedLocation(StartupSource source) => source switch
    {
        StartupSource.RunUser => (RegHive.CurrentUser, ApprovedRoot + @"\Run"),
        StartupSource.RunMachine => (RegHive.LocalMachine, ApprovedRoot + @"\Run"),
        StartupSource.RunMachine32 => (RegHive.LocalMachine, ApprovedRoot + @"\Run32"),
        StartupSource.StartupFolderUser => (RegHive.CurrentUser, ApprovedRoot + @"\StartupFolder"),
        StartupSource.StartupFolderAllUsers => (RegHive.LocalMachine, ApprovedRoot + @"\StartupFolder"),
        _ => null,
    };

    // ---------------------------------------------------------------- Listing

    public List<StartupEntry> GetEntries()
    {
        var list = new List<StartupEntry>();
        AddRunKey(list, RegHive.CurrentUser, RunPath, StartupSource.RunUser);
        AddRunKey(list, RegHive.LocalMachine, RunPath, StartupSource.RunMachine);
        AddRunKey(list, RegHive.LocalMachine, Run32Path, StartupSource.RunMachine32);
        foreach (var (folder, source) in _folders) AddFolder(list, folder, source);
        AddScheduledTasks(list);
        return list.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private void AddRunKey(List<StartupEntry> list, RegHive hive, string path, StartupSource source)
    {
        var approved = ApprovedLocation(source)!.Value;
        foreach (var (name, value) in _reg.GetValues(hive, path))
        {
            if (string.IsNullOrWhiteSpace(value.Data) || string.IsNullOrEmpty(name)) continue;
            var exe = ExtractExecutable(value.Data);
            list.Add(new StartupEntry
            {
                Name = name,
                Command = value.Data,
                ExecutablePath = exe,
                Publisher = GetPublisher(exe),
                Source = source,
                Enabled = IsApprovedEnabled(_reg.GetValue(approved.Hive, approved.Path, name)),
                Impact = EstimateImpact(exe),
            });
        }
    }

    private void AddFolder(List<StartupEntry> list, string folder, StartupSource source)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
        var approved = ApprovedLocation(source)!.Value;
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
            var target = file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? ResolveShortcut(file) ?? file : file;
            list.Add(new StartupEntry
            {
                Name = fileName,
                Command = target,
                ExecutablePath = target,
                Publisher = GetPublisher(target),
                Source = source,
                Enabled = IsApprovedEnabled(_reg.GetValue(approved.Hive, approved.Path, fileName)),
                Impact = EstimateImpact(target),
            });
        }
    }

    private sealed record TaskDto(string? Name, string? Path, string? State, string? Author, string? Execute, string? Arguments);

    private void AddScheduledTasks(List<StartupEntry> list)
    {
        // Only third-party tasks: anything under \Microsoft\ is part of Windows and is never offered.
        const string script = """
            $items = Get-ScheduledTask | Where-Object {
                $_.TaskPath -notlike '\Microsoft\*' -and
                ($_.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' })
            } | ForEach-Object {
                $a = $_.Actions | Select-Object -First 1
                [pscustomobject]@{ Name = $_.TaskName; Path = $_.TaskPath; State = [string]$_.State;
                                   Author = $_.Author; Execute = $a.Execute; Arguments = $a.Arguments }
            }
            ConvertTo-Json -InputObject @($items) -Compress
            """;
        var r = _cmd.RunPowerShell(script, 60_000);
        if (!r.Success || string.IsNullOrWhiteSpace(r.StdOut)) return;

        List<TaskDto>? tasks;
        try { tasks = JsonSerializer.Deserialize<List<TaskDto>>(r.StdOut.Trim()); }
        catch (JsonException) { return; }

        foreach (var t in tasks ?? new())
        {
            if (string.IsNullOrEmpty(t.Name)) continue;
            var exe = Environment.ExpandEnvironmentVariables((t.Execute ?? "").Trim('"'));
            var publisher = GetPublisher(exe);
            list.Add(new StartupEntry
            {
                Name = t.Name,
                TaskPath = t.Path ?? "\\",
                Command = $"{t.Execute} {t.Arguments}".Trim(),
                ExecutablePath = exe,
                Publisher = publisher.Length > 0 ? publisher : t.Author ?? "",
                Source = StartupSource.ScheduledTask,
                Enabled = !string.Equals(t.State, "Disabled", StringComparison.OrdinalIgnoreCase),
                Impact = EstimateImpact(exe),
            });
        }
    }

    // ---------------------------------------------------------------- Toggling

    /// <summary>Enables or disables an entry. Records the original state the first time so Revert All works.</summary>
    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        var backupId = BackupPrefix + entry.Id;

        if (entry.Source == StartupSource.ScheduledTask)
        {
            _backups.TryAdd(new TweakBackup
            {
                Id = backupId,
                DisplayName = $"Startup: {entry.Name}",
                State = { ["Kind"] = "Task", ["Name"] = entry.Name, ["TaskPath"] = entry.TaskPath, ["Enabled"] = entry.Enabled ? "1" : "0" },
            });
            SetTaskEnabled(entry.Name, entry.TaskPath, enabled);
        }
        else
        {
            var loc = ApprovedLocation(entry.Source)!.Value;
            var current = _reg.GetValue(loc.Hive, loc.Path, entry.Name);
            _backups.TryAdd(new TweakBackup
            {
                Id = backupId,
                DisplayName = $"Startup: {entry.Name}",
                State = { ["Kind"] = "Approved" },
                Registry = { new RegistrySnapshot { Hive = loc.Hive, Path = loc.Path, Name = entry.Name, Existed = current is not null, Value = current } },
            });
            _reg.SetValue(loc.Hive, loc.Path, entry.Name, MakeApprovedValue(enabled, DateTime.UtcNow));
        }

        entry.Enabled = enabled;
        _log.Add("startup", $"{(enabled ? "Enabled" : "Disabled")} startup item: {entry.Name}", ChangeAction.Applied,
            new[] { $"Source: {entry.SourceText}", $"Command: {entry.Command}", "Nothing was deleted; the item can be re-enabled at any time." });
    }

    /// <summary>Restores one startup item from its backup (used by Revert All).</summary>
    public void Restore(TweakBackup backup)
    {
        if (backup.State.GetValueOrDefault("Kind") == "Task")
        {
            SetTaskEnabled(backup.State["Name"], backup.State.GetValueOrDefault("TaskPath") ?? "\\", backup.State.GetValueOrDefault("Enabled") == "1");
        }
        else
        {
            foreach (var s in backup.Registry)
            {
                if (s.Existed && s.Value is not null) _reg.SetValue(s.Hive, s.Path, s.Name, s.Value);
                else _reg.DeleteValue(s.Hive, s.Path, s.Name);
            }
        }
        _backups.Remove(backup.Id);
        _log.Add("startup", $"Restored original startup state: {backup.DisplayName}", ChangeAction.Reverted);
    }

    private void SetTaskEnabled(string name, string taskPath, bool enabled)
    {
        static string Q(string s) => "'" + s.Replace("'", "''") + "'";
        var verb = enabled ? "Enable-ScheduledTask" : "Disable-ScheduledTask";
        var r = _cmd.RunPowerShell($"{verb} -TaskName {Q(name)} -TaskPath {Q(taskPath)} | Out-Null", 30_000);
        if (!r.Success)
            throw new InvalidOperationException($"Task Scheduler refused the change: {FirstLine(r.ErrorText)}");
    }

    // ---------------------------------------------------------------- Helpers

    /// <summary>Pulls the executable out of a command line like "\"C:\x\app.exe\" --minimized".</summary>
    internal static string ExtractExecutable(string command)
    {
        var cmd = Environment.ExpandEnvironmentVariables(command.Trim());
        if (cmd.StartsWith('"'))
        {
            var end = cmd.IndexOf('"', 1);
            return end > 1 ? cmd[1..end] : cmd.Trim('"');
        }
        var exeIdx = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIdx > 0) return cmd[..(exeIdx + 4)];
        var space = cmd.IndexOf(' ');
        return space > 0 ? cmd[..space] : cmd;
    }

    private static string GetPublisher(string exe)
    {
        try
        {
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return "";
            return FileVersionInfo.GetVersionInfo(exe).CompanyName?.Trim() ?? "";
        }
        catch { return ""; }
    }

    /// <summary>
    /// A rough estimate only. Windows' real measurement isn't available to apps, so we use the program's size
    /// on disk as a proxy. The UI labels this as an estimate.
    /// </summary>
    internal static StartupImpact EstimateImpact(string exe)
    {
        try
        {
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return StartupImpact.Unknown;
            var size = new FileInfo(exe).Length;
            return size switch
            {
                < 2 * 1024 * 1024 => StartupImpact.Low,
                < 30 * 1024 * 1024 => StartupImpact.Medium,
                _ => StartupImpact.High,
            };
        }
        catch { return StartupImpact.Unknown; }
    }

    private static string? ResolveShortcut(string lnk)
    {
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return null;
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic link = shell.CreateShortcut(lnk);
            string target = link.TargetPath;
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch { return null; }
    }

    private static string FirstLine(string s) => s.Split('\n').FirstOrDefault()?.Trim() ?? s;
}
