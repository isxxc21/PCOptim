using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace PCOpti.Services;

public sealed class AppSettings
{
    public string Accent { get; set; } = "Cyan";
    /// <summary>Offer to create a System Restore point before the first change of each session.</summary>
    public bool AskForRestorePoint { get; set; } = true;
    /// <summary>Off by default: no network traffic unless the user opts in.</summary>
    public bool CheckForUpdatesOnStartup { get; set; }
    /// <summary>The first-run "this modifies system settings" notice has been accepted.</summary>
    public bool AcceptedDisclaimer { get; set; }
}

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path = Path.Combine(AppInfo.DataFolder, "settings.json");

    public AppSettings Current { get; }

    public SettingsService()
    {
        try
        {
            Current = File.Exists(_path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new() : new();
        }
        catch
        {
            Current = new();
        }
    }

    public void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(Current, Json)); } catch { }
    }
}

public sealed record AccentOption(string Name, Color Color)
{
    public SolidColorBrush Brush => new(Color);
}

/// <summary>Swaps the accent colour at runtime by replacing the brushes the theme references dynamically.</summary>
public static class ThemeService
{
    public static readonly IReadOnlyList<AccentOption> Accents = new[]
    {
        new AccentOption("Cyan", Color.FromRgb(0x00, 0xD1, 0xFF)),
        new AccentOption("Violet", Color.FromRgb(0xA7, 0x8B, 0xFA)),
        new AccentOption("Green", Color.FromRgb(0x3D, 0xDC, 0x84)),
        new AccentOption("Orange", Color.FromRgb(0xFF, 0x9F, 0x43)),
        new AccentOption("Pink", Color.FromRgb(0xFF, 0x5C, 0x9A)),
    };

    public static void Apply(string accentName)
    {
        var accent = Accents.FirstOrDefault(a => a.Name == accentName) ?? Accents[0];
        var res = Application.Current.Resources;
        res["AccentBrush"] = Frozen(accent.Color);
        res["AccentDimBrush"] = Frozen(Color.FromArgb(0x26, accent.Color.R, accent.Color.G, accent.Color.B));
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
