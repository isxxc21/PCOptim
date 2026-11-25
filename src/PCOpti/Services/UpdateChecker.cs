using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PCOpti.Services;

public sealed record UpdateInfo(bool IsNewer, Version Latest, string TagName, string Url);

/// <summary>
/// The ONLY network call PCOpti makes. It asks the public GitHub Releases API for the latest version and only
/// tells the user. It never downloads or installs anything. No personal data is sent.
/// </summary>
public sealed class UpdateChecker
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCOpti-UpdateCheck", AppInfo.Version.ToString(3)));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        using var response = await Http.GetAsync(AppInfo.LatestReleaseApi, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException("No releases have been published yet.");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
        var url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() ?? AppInfo.ReleasesUrl : AppInfo.ReleasesUrl;
        var latest = ParseVersion(tag) ?? throw new InvalidOperationException($"Unrecognised release tag \"{tag}\".");
        return new UpdateInfo(latest > AppInfo.Version, latest, tag, url);
    }

    /// <summary>Parses "v1.2.3", "1.2", "v2.0.0-beta.1" into a Version (pre-release suffix ignored).</summary>
    internal static Version? ParseVersion(string tag)
    {
        var s = tag.Trim().TrimStart('v', 'V');
        var dash = s.IndexOfAny(new[] { '-', '+' });
        if (dash >= 0) s = s[..dash];
        if (!Version.TryParse(s.Contains('.') ? s : s + ".0", out var v)) return null;
        return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
    }
}
