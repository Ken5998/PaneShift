using PaneShift.Core;

namespace PaneShift.Core.Tests;

public sealed class RuntimeSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PaneShift.Tests", Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(directory, "settings.json");

    private SettingsStore Write(string json)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, json);
        return new(SettingsPath);
    }

    [Fact]
    public void SuccessfulReloadChangesNextLayoutAndResetsHalfCycle()
    {
        var runtime = new RuntimeSettings();
        var initial = runtime.Current;
        Assert.Equal(0, runtime.Repetition.Next(42, WindowAction.LeftHalf, 3));
        Assert.Equal(1, runtime.Repetition.Next(42, WindowAction.LeftHalf, 3));
        var store = Write("{\"gapPixels\":12,\"applyGapToScreenEdges\":true}");
        Assert.Null(runtime.Reload(store));
        Assert.Equal(0, initial.GapPixels);
        Assert.Equal(12, runtime.Current.GapPixels);
        Assert.True(runtime.Current.ApplyGapToScreenEdges);
        int index = runtime.Repetition.Next(42, WindowAction.LeftHalf, 3);
        Assert.Equal(0, index);
        var area = new PixelRect(-1920, 0, 1920, 1080);
        var ideal = WindowGeometry.CalculateHalf(WindowAction.LeftHalf, area, HalfActionCycle.Sizes[index]);
        Assert.Equal(new PixelRect(-1908, 12, 942, 1056),
            WindowGaps.Apply(ideal, area, runtime.Current.GapPixels, runtime.Current.ApplyGapToScreenEdges));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"gapPixels\":-1}")]
    [InlineData("{\"gapPixels\":20,\"repeatedCommands\":{\"halfActions\":\"unsupported\"}}")]
    [InlineData("{\"gapPixels\":20,\"applyGapToScreenEdges\":null}")]
    public void FailedReloadPreservesExactSnapshotAndRepetition(string json)
    {
        var active = new PaneShiftSettings { GapPixels = 12, ApplyGapToScreenEdges = true };
        var runtime = new RuntimeSettings(active);
        runtime.Repetition.Next(1, WindowAction.LeftHalf, 3);
        runtime.Repetition.Next(1, WindowAction.LeftHalf, 3);
        var store = Write(json);
        Assert.NotNull(runtime.Reload(store));
        Assert.Same(active, runtime.Current);
        Assert.Equal(2, runtime.Repetition.Next(1, WindowAction.LeftHalf, 3));
        Assert.Equal(json, File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void MissingFileOnReloadDoesNotCreateDefaults()
    {
        var active = new PaneShiftSettings { GapPixels = 12 };
        var runtime = new RuntimeSettings(active);
        Assert.NotNull(runtime.Reload(new SettingsStore(SettingsPath)));
        Assert.Same(active, runtime.Current);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void InaccessibleFileKeepsActiveSettings()
    {
        var store = Write("{\"gapPixels\":20}");
        using var locked = new FileStream(SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var active = new PaneShiftSettings { GapPixels = 12 };
        var runtime = new RuntimeSettings(active);
        Assert.NotNull(runtime.Reload(store));
        Assert.Same(active, runtime.Current);
    }

    [Theory]
    [InlineData("{}", 0, false)]
    [InlineData("{\"gapPixels\":11,\"futureProperty\":true}", 11, false)]
    [InlineData("{\"applyGapToScreenEdges\":true,\"repeatedCommands\":{}}", 0, true)]
    public void ReloadKeepsBackwardsCompatibleDefaults(string json, int gap, bool edges)
    {
        var runtime = new RuntimeSettings(new() { GapPixels = 12 });
        Assert.Null(runtime.Reload(Write(json)));
        Assert.Equal(gap, runtime.Current.GapPixels);
        Assert.Equal(edges, runtime.Current.ApplyGapToScreenEdges);
        Assert.Equal(RepeatBehavior.CycleSizes, runtime.Current.RepeatedCommands.HalfActions);
    }

    [Fact]
    public void CandidateIsSeparateFromCommitAndInvalidCommitIsRejected()
    {
        var runtime = new RuntimeSettings();
        var initial = runtime.Current;
        var candidate = Write("{\"gapPixels\":12}").LoadCandidate();
        Assert.Null(candidate.Error);
        Assert.Same(initial, runtime.Current);
        Assert.Throws<ArgumentOutOfRangeException>(() => runtime.ReplaceCurrent(new() { GapPixels = -1 }));
        Assert.Same(initial, runtime.Current);
        runtime.ReplaceCurrent(candidate.Settings!);
        Assert.Equal(12, runtime.Current.GapPixels);
    }

    [Fact]
    public void SuccessfulReloadAfterFailureCanApplyAndDoesNotRewriteFile()
    {
        var runtime = new RuntimeSettings(new() { GapPixels = 12 });
        Assert.NotNull(runtime.Reload(Write("{")));
        var store = Write("{\"gapPixels\":20}");
        File.SetLastWriteTimeUtc(SettingsPath, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var modified = File.GetLastWriteTimeUtc(SettingsPath);
        Assert.Null(runtime.Reload(store));
        Assert.Equal(20, runtime.Current.GapPixels);
        Assert.Equal(modified, File.GetLastWriteTimeUtc(SettingsPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
