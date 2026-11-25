namespace PCOpti.Models;

public enum ChangeAction { Applied, Reverted, Ran, Cleaned, Failed, Info }

/// <summary>One line in the user-visible change log.</summary>
public sealed class ChangeEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    /// <summary>Tweak id, or an area such as "startup", "network", "cleaner".</summary>
    public string Source { get; set; } = "";
    public string Title { get; set; } = "";
    public ChangeAction Action { get; set; }
    /// <summary>Exact details, e.g. "HKCU\...\EnableTransparency: 1 -> 0".</summary>
    public List<string> Details { get; set; } = new();

    public string TimeText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string DetailsText => string.Join(Environment.NewLine, Details);
}
