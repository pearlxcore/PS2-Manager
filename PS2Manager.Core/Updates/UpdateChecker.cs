using System.Net.Http.Headers;
using System.Text.Json;

namespace PS2Manager.Core.Updates;

public sealed record UpdateInfo(Version Version, string Tag, string PageUrl, string? Notes);

/// <summary>Checks GitHub for a newer published release.</summary>
public static class UpdateChecker
{
    public const string Repo = "pearlxcore/PS2-Manager";
    public const string ReleasesUrl = "https://github.com/" + Repo + "/releases";

    /// <summary>Returns the latest release when it's newer than <paramref name="current"/>, otherwise null.</summary>
    public static async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/releases/latest");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        using var resp = await Http.Client.SendAsync(req, cts.Token);
        if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"GitHub returned {(int)resp.StatusCode}");

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(cts.Token), cancellationToken: cts.Token);
        var root = doc.RootElement;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        string url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? ReleasesUrl : ReleasesUrl;
        string? notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;

        return ParseTag(tag) is { } latest && IsNewer(latest, current) ? new UpdateInfo(latest, tag, url, notes) : null;
    }

    /// <summary>"v2.1.0", "2.1", "V3.0.1-beta" → a Version (pre-release suffixes are ignored).</summary>
    public static Version? ParseTag(string tag)
    {
        var t = tag.Trim().TrimStart('v', 'V');
        int cut = t.IndexOfAny(['-', '+', ' ']);
        if (cut >= 0) t = t[..cut];
        if (!t.Contains('.')) t += ".0";
        return Version.TryParse(t, out var v) ? Normalize(v) : null;
    }

    /// <summary>Compares major.minor.build only, so 2.1.0 and 2.1.0.0 are equal.</summary>
    public static bool IsNewer(Version latest, Version current) => Normalize(latest) > Normalize(current);

    private static Version Normalize(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));
}
