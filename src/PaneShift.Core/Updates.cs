using System.Text.Json;
using System.Text.RegularExpressions;

namespace PaneShift.Core;

public sealed record UpdateSettings
{
    public bool CheckAutomatically { get; init; }
}

public sealed record ReleaseAsset(string Name, Uri DownloadUri, long Size, string Sha256);
public sealed record AvailableRelease(Version Version, string Tag, Uri PageUri, string Notes,
    ReleaseAsset? Installer, ReleaseAsset Portable);

/// <summary>Only the fixed repository's stable release assets can be offered or executed.</summary>
public static partial class ReleaseUpdates
{
    public const string Repository = "Ken5998/PaneShift";
    public static readonly Uri LatestUri = new($"https://api.github.com/repos/{Repository}/releases/latest");
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    public static bool IsDue(DateTimeOffset now, DateTimeOffset? lastAttempt) =>
        lastAttempt is null || now < lastAttempt || now - lastAttempt >= CheckInterval;

    public static Version StableVersion(string value)
    {
        if (!StablePattern().IsMatch(value) || !Version.TryParse(value, out var version))
            throw new ArgumentException("Expected a stable three-part version.");
        return version;
    }

    public static AvailableRelease? Parse(string json, Version current)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!tag.StartsWith('v')) throw new ArgumentException("Unexpected release tag.");
        var version = StableVersion(tag[1..]);
        if (version <= new Version(current.Major, current.Minor, Math.Max(0, current.Build))) return null;
        var page = new Uri($"https://github.com/{Repository}/releases/tag/{tag}");
        if (root.GetProperty("html_url").GetString() != page.AbsoluteUri) throw new ArgumentException("Unexpected release repository.");
        ReleaseAsset? installer = null, portable = null;
        string installerName = $"PaneShift-{version}-Setup-x64.msi", portableName = $"PaneShift-{version}-win-x64.zip";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            if (name != installerName && name != portableName) continue;
            string expected = $"https://github.com/{Repository}/releases/download/{tag}/{name}";
            if (asset.GetProperty("browser_download_url").GetString() != expected) throw new ArgumentException("Unexpected asset URL.");
            string digest = asset.TryGetProperty("digest", out var hash) ? hash.GetString() ?? "" : "";
            if (!DigestPattern().IsMatch(digest)) throw new ArgumentException("Release asset has no valid GitHub SHA-256 digest.");
            long size = asset.GetProperty("size").GetInt64();
            if (size <= 0 || size > 512L * 1024 * 1024) throw new ArgumentException("Unexpected release asset size.");
            var result = new ReleaseAsset(name!, new Uri(expected), size, digest[7..].ToLowerInvariant());
            if (name == installerName) { if (installer is not null) throw new ArgumentException("Duplicate installer asset."); installer = result; }
            else { if (portable is not null) throw new ArgumentException("Duplicate ZIP asset."); portable = result; }
        }
        if (portable is null) throw new ArgumentException("The release has no verified portable ZIP.");
        return new(version, tag, page, root.TryGetProperty("body", out var notes) ? notes.GetString() ?? "" : "", installer, portable);
    }

    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z")]
    private static partial Regex StablePattern();
    [GeneratedRegex(@"^sha256:[a-fA-F0-9]{64}\z")]
    private static partial Regex DigestPattern();
}
