using System.Reflection;

namespace PCOpti;

/// <summary>Static facts about this build, shown on the About page and used by the update check.</summary>
public static class AppInfo
{
    public const string Name = "PCOpti";

    public const string GitHubOwner = "isxxc21";
    public const string GitHubRepo = "PCOptim";

    public static string RepoUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}";
    public static string ReleasesUrl => $"{RepoUrl}/releases";
    public static string LatestReleaseApi => $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    public static Version Version
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    public static string VersionText => $"v{Version.ToString(3)}";

    /// <summary>Folder for backups, the change log and settings: %LocalAppData%\PCOpti.</summary>
    public static string DataFolder
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
