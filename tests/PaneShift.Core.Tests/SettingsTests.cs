using PaneShift.Core;

namespace PaneShift.Core.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PaneShift.Tests", Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(directory, "settings.json");

    private SettingsLoadResult Load(string json)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, json);
        return new SettingsStore(SettingsPath).LoadOrCreate();
    }

    [Fact]
    public void MissingFileCreatesDefaultsAndSubsequentLoadDoesNotWrite()
    {
        var store = new SettingsStore(SettingsPath);
        var first = store.LoadOrCreate();
        Assert.Null(first.Warning);
        Assert.Equal(new PaneShiftSettings(), first.Settings);
        string json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"gapPixels\": 0", json);
        Assert.Contains("\"halfActions\": \"cycleSizes\"", json);
        File.SetLastWriteTimeUtc(SettingsPath, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var timestamp = File.GetLastWriteTimeUtc(SettingsPath);
        Assert.Equal(first, store.LoadOrCreate());
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(SettingsPath));
        Assert.Equal(json, File.ReadAllText(SettingsPath));
    }

    [Theory]
    [InlineData("{}", 0, false)]
    [InlineData("{\"gapPixels\":12}", 12, false)]
    [InlineData("{\"applyGapToScreenEdges\":true,\"repeatedCommands\":{}}", 0, true)]
    [InlineData("{\"gapPixels\":11,\"applyGapToScreenEdges\":true,\"futureSetting\":42}", 11, true)]
    public void MissingAndUnknownPropertiesAreCompatible(string json, int gap, bool edges)
    {
        var result = Load(json);
        Assert.Null(result.Warning);
        Assert.Equal(gap, result.Settings.GapPixels);
        Assert.Equal(edges, result.Settings.ApplyGapToScreenEdges);
        Assert.Equal(RepeatBehavior.CycleSizes, result.Settings.RepeatedCommands.HalfActions);
        Assert.Equal(json, File.ReadAllText(SettingsPath));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"gapPixels\":-1}")]
    [InlineData("{\"gapPixels\":\"twelve\"}")]
    [InlineData("{\"repeatedCommands\":null}")]
    [InlineData("{\"repeatedCommands\":{\"halfActions\":\"unknown\"}}")]
    [InlineData("{\"repeatedCommands\":{\"halfActions\":0}}")]
    public void MalformedOrInvalidSettingsFallBackWithoutOverwriting(string json)
    {
        var result = Load(json);
        Assert.NotNull(result.Warning);
        Assert.Equal(new PaneShiftSettings(), result.Settings);
        Assert.Equal(json, File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void UnwritableDestinationReturnsDefaultsAndWarning()
    {
        Directory.CreateDirectory(SettingsPath);
        var result = new SettingsStore(SettingsPath).LoadOrCreate();
        Assert.NotNull(result.Warning);
        Assert.Equal(new PaneShiftSettings(), result.Settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
