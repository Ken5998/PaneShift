using System.Text.Json;
using PaneShift.Core;

namespace PaneShift.Core.Tests;

public sealed class ReleaseUpdateTests
{
    private static string Release(string version = "0.4.0", bool draft = false, bool prerelease = false,
        string owner = "Ken5998/PaneShift", string digest = "sha256:" + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") =>
        JsonSerializer.Serialize(new { draft, prerelease, tag_name = "v" + version,
            html_url = $"https://github.com/{owner}/releases/tag/v{version}", body = "Release notes",
            assets = new[] { new { name = $"PaneShift-{version}-Setup-x64.msi", size = 100,
                browser_download_url = $"https://github.com/{owner}/releases/download/v{version}/PaneShift-{version}-Setup-x64.msi", digest },
                new { name = $"PaneShift-{version}-win-x64.zip", size = 100,
                    browser_download_url = $"https://github.com/{owner}/releases/download/v{version}/PaneShift-{version}-win-x64.zip", digest } } });

    [Fact]
    public void OnlyNewStableReleaseIsOfferedWithExactAssets()
    {
        var result = ReleaseUpdates.Parse(Release(), new(0, 3, 0));
        Assert.Equal(new Version(0, 4, 0), result!.Version);
        Assert.Equal("PaneShift-0.4.0-Setup-x64.msi", result.Installer!.Name);
        Assert.Equal("PaneShift-0.4.0-win-x64.zip", result.Portable.Name);
        Assert.Null(ReleaseUpdates.Parse(Release("0.3.0"), new(0, 3, 0, 0)));
        Assert.Null(ReleaseUpdates.Parse(Release("0.1.1"), new(0, 3, 0)));
        Assert.Null(ReleaseUpdates.Parse(Release(draft: true), new(0, 3, 0)));
        Assert.Null(ReleaseUpdates.Parse(Release(prerelease: true), new(0, 3, 0)));
    }

    [Theory]
    [InlineData("0.4.0-rc.1")]
    [InlineData("01.4.0")]
    [InlineData("0.4")]
    [InlineData("0.4.0/x")]
    [InlineData("0.4.0\n")]
    public void MalformedStableTagsAreRejected(string version) =>
        Assert.Throws<ArgumentException>(() => ReleaseUpdates.Parse(Release(version), new(0, 3, 0)));

    [Fact]
    public void ForeignRepositoryAndMissingHashAreRejected()
    {
        Assert.Throws<ArgumentException>(() => ReleaseUpdates.Parse(Release(owner: "Other/Project"), new(0, 3, 0)));
        Assert.Throws<ArgumentException>(() => ReleaseUpdates.Parse(Release(digest: ""), new(0, 3, 0)));
        Assert.Throws<ArgumentException>(() => ReleaseUpdates.Parse(Release().Replace("releases/download/", "outside/"), new(0, 3, 0)));
    }

    [Fact]
    public void CadenceUsesAttemptsAndHandlesClockRollback()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(ReleaseUpdates.IsDue(now, null));
        Assert.False(ReleaseUpdates.IsDue(now, now));
        Assert.False(ReleaseUpdates.IsDue(now, now.AddHours(-23)));
        Assert.True(ReleaseUpdates.IsDue(now, now.AddHours(-24)));
        Assert.True(ReleaseUpdates.IsDue(now, now.AddHours(1)));
    }

    [Fact]
    public void OldConfigurationDefaultsOffAndEnabledStateRoundTrips()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{\"gapPixels\":8}");
            var store = new SettingsStore(path);
            Assert.False(store.LoadCandidate().Settings!.Updates.CheckAutomatically);
            store.Save(store.LoadCandidate().Settings! with { Updates = new() { CheckAutomatically = true } });
            Assert.True(store.LoadCandidate().Settings!.Updates.CheckAutomatically);
            Assert.Equal(8, store.LoadCandidate().Settings!.GapPixels);
            File.WriteAllText(path, "{\"updates\":null}");
            Assert.Null(store.LoadCandidate().Settings);
            Assert.NotNull(store.LoadCandidate().Error);
        }
        finally { Directory.Delete(directory, true); }
    }
}
