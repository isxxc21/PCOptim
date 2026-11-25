using System.Diagnostics;
using PCOpti.Models;

namespace PCOpti.Services;

public sealed record RunningApp(string ProcessName, string Title, string Path, int ProcessCount, long MemoryBytes, bool HasWindow, int MainPid);

/// <summary>
/// Game Boost: close chosen background apps (and reopen them later) and raise a game's CPU priority.
/// Nothing here is permanent: priority resets when the game restarts, and closed apps can be reopened.
/// </summary>
public sealed class GameBoostService
{
    // Never offered: closing these would break Windows or PCOpti itself.
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "dwm", "csrss", "winlogon", "wininit", "lsass", "services", "smss", "svchost", "sihost",
        "ctfmon", "fontdrvhost", "taskhostw", "RuntimeBroker", "SearchHost", "SearchApp", "StartMenuExperienceHost",
        "ShellExperienceHost", "TextInputHost", "ApplicationFrameHost", "SecurityHealthSystray", "MsMpEng",
        "audiodg", "conhost", "dllhost", "WmiPrvSE", "System", "Idle", "Registry", "LockApp", "PCOpti",
        "ShellHost", "Widgets", "WidgetService", "smartscreen", "SystemSettings", "UserOOBEBroker",
    };

    private readonly IChangeLog _log;
    private readonly List<string> _closedPaths = new();
    private readonly Dictionary<int, ProcessPriorityClass> _originalPriority = new();

    public GameBoostService(IChangeLog log) => _log = log;

    public IReadOnlyList<string> ClosedApps => _closedPaths;

    /// <summary>User apps in this session, grouped by program, excluding Windows components.</summary>
    public List<RunningApp> GetRunningApps()
    {
        var session = Process.GetCurrentProcess().SessionId;
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var apps = new List<RunningApp>();

        foreach (var group in Process.GetProcesses().Where(p => SafeSession(p) == session).GroupBy(p => p.ProcessName))
        {
            if (Protected.Contains(group.Key)) continue;
            string path = "";
            string title = "";
            long mem = 0;
            bool hasWindow = false;
            int mainPid = 0;
            foreach (var p in group)
            {
                try
                {
                    mem += p.WorkingSet64;
                    if (path.Length == 0) path = p.MainModule?.FileName ?? "";
                    if (p.MainWindowHandle != IntPtr.Zero && mainPid == 0)
                    {
                        hasWindow = true; mainPid = p.Id; title = p.MainWindowTitle;
                    }
                }
                catch { /* access denied for some processes */ }
            }
            if (path.Length == 0 || path.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) continue;
            if (mainPid == 0) mainPid = group.First().Id;
            apps.Add(new RunningApp(group.Key, title, path, group.Count(), mem, hasWindow, mainPid));
            foreach (var p in group) p.Dispose();
        }
        return apps.OrderByDescending(a => a.MemoryBytes).ToList();
    }

    private static int SafeSession(Process p)
    {
        try { return p.SessionId; } catch { return -1; }
    }

    /// <summary>
    /// Asks each app to close normally (like clicking X), so they can save. Returns names that didn't close.
    /// Apps that only live in the system tray can't be asked nicely; they are reported back for the user to decide.
    /// </summary>
    public List<string> CloseApps(IEnumerable<RunningApp> apps, bool force)
    {
        var stillRunning = new List<string>();
        foreach (var app in apps)
        {
            var procs = Process.GetProcessesByName(app.ProcessName);
            foreach (var p in procs)
            {
                try
                {
                    if (force) p.Kill(entireProcessTree: true);
                    else if (p.MainWindowHandle != IntPtr.Zero) p.CloseMainWindow();
                }
                catch { }
            }

            var deadline = DateTime.UtcNow.AddSeconds(force ? 3 : 6);
            while (DateTime.UtcNow < deadline && Process.GetProcessesByName(app.ProcessName).Length > 0)
                Thread.Sleep(250);

            if (Process.GetProcessesByName(app.ProcessName).Length > 0)
            {
                stillRunning.Add(app.ProcessName);
            }
            else
            {
                if (!_closedPaths.Contains(app.Path, StringComparer.OrdinalIgnoreCase)) _closedPaths.Add(app.Path);
                _log.Add("boost", $"Game Boost {(force ? "force-closed" : "closed")}: {app.ProcessName}", ChangeAction.Ran, new[] { app.Path });
            }
            foreach (var p in procs) p.Dispose();
        }
        return stillRunning;
    }

    /// <summary>
    /// Starts the closed apps again. They're launched through Explorer so they run as a normal user,
    /// not with PCOpti's administrator rights. Command-line arguments aren't restored.
    /// </summary>
    public int ReopenClosedApps()
    {
        var count = 0;
        foreach (var path in _closedPaths.ToList())
        {
            try
            {
                if (!File.Exists(path)) continue;
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = false })?.Dispose();
                count++;
            }
            catch { }
        }
        _log.Add("boost", $"Game Boost reopened {count} app(s)", ChangeAction.Reverted, _closedPaths);
        _closedPaths.Clear();
        return count;
    }

    /// <summary>Sets a process to High priority (never Realtime, which can freeze input and audio).</summary>
    public void RaisePriority(int pid)
    {
        using var p = Process.GetProcessById(pid);
        if (!_originalPriority.ContainsKey(pid)) _originalPriority[pid] = p.PriorityClass;
        p.PriorityClass = ProcessPriorityClass.High;
        _log.Add("boost", $"Raised priority of {p.ProcessName} to High", ChangeAction.Applied,
            new[] { $"PID {pid}: {_originalPriority[pid]} -> High", "Resets automatically when the game closes." });
    }

    public void RestorePriority(int pid)
    {
        using var p = Process.GetProcessById(pid);
        var original = _originalPriority.GetValueOrDefault(pid, ProcessPriorityClass.Normal);
        p.PriorityClass = original;
        _originalPriority.Remove(pid);
        _log.Add("boost", $"Restored priority of {p.ProcessName}", ChangeAction.Reverted, new[] { $"PID {pid}: High -> {original}" });
    }

    public bool IsBoosted(int pid) => _originalPriority.ContainsKey(pid);
}
