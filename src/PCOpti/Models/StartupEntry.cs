namespace PCOpti.Models;

public enum StartupSource
{
    RunUser,          // HKCU\...\Run
    RunMachine,       // HKLM\...\Run
    RunMachine32,     // HKLM\SOFTWARE\WOW6432Node\...\Run
    StartupFolderUser,
    StartupFolderAllUsers,
    ScheduledTask,    // Task Scheduler task with an "At log on" trigger
}

public enum StartupImpact { Unknown, Low, Medium, High }

/// <summary>One program that starts when you sign in.</summary>
public sealed class StartupEntry
{
    /// <summary>Stable id, e.g. "RunUser|Discord". Used as the backup key.</summary>
    public string Id => $"{Source}|{TaskPath}{Name}";
    public string Name { get; init; } = "";
    public string Command { get; init; } = "";
    /// <summary>Resolved executable path (may be empty if it couldn't be resolved).</summary>
    public string ExecutablePath { get; init; } = "";
    public string Publisher { get; init; } = "";
    public StartupSource Source { get; init; }
    /// <summary>Folder in Task Scheduler (only for scheduled tasks), e.g. "\".</summary>
    public string TaskPath { get; init; } = "";
    public bool Enabled { get; set; }
    public StartupImpact Impact { get; init; }

    public string SourceText => Source switch
    {
        StartupSource.RunUser => "Registry (current user)",
        StartupSource.RunMachine => "Registry (all users)",
        StartupSource.RunMachine32 => "Registry (all users, 32-bit)",
        StartupSource.StartupFolderUser => "Startup folder (current user)",
        StartupSource.StartupFolderAllUsers => "Startup folder (all users)",
        StartupSource.ScheduledTask => "Task Scheduler (at log on)",
        _ => Source.ToString(),
    };
}
