using System.Collections.Immutable;

namespace PaneShift.Core.Tests;

public class HotkeySettingsTests
{
    [Theory]
    [InlineData("Ctrl+Alt+D", "Ctrl + Alt + D")]
    [InlineData("Ctrl+Alt+Left", "Ctrl + Alt + Left")]
    [InlineData("Ctrl+Shift+Win+Up", "Ctrl + Shift + Win + Up")]
    [InlineData("win + shift + ctrl + up", "Ctrl + Shift + Win + Up")]
    public void ChordsRoundTripCanonically(string input, string display)
    {
        var chord = HotkeyChord.Parse(input);
        Assert.Equal(display, chord.Display);
        Assert.Equal(chord, HotkeyChord.Parse(chord.ToString()));
    }

    [Theory]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Ctrl+D")]
    [InlineData("D")]
    [InlineData("Ctrl+F12")]
    public void InvalidChordsAreRejected(string input) => Assert.Throws<ArgumentException>(() => HotkeyChord.Parse(input));

    [Fact]
    public void MissingMapRetainsExactDefaults() => Assert.Equal(PaneShiftConfiguration.Default.Hotkeys.OrderBy(b => b.Action), HotkeySettings.Resolve(new()).OrderBy(b => b.Action));

    [Fact]
    public void NullDisablesButMissingActionsStillReceiveDefaults()
    {
        var settings = new PaneShiftSettings { Hotkeys = ImmutableSortedDictionary<string, string?>.Empty.Add("leftHalf", null) };
        Assert.DoesNotContain(HotkeySettings.Resolve(settings), b => b.Action == WindowAction.LeftHalf);
        Assert.Equal(17, HotkeySettings.Resolve(settings).Count);
    }

    [Fact]
    public void DuplicateAssignmentsAreRejectedBeforeActivation()
    {
        var settings = new PaneShiftSettings { Hotkeys = ImmutableSortedDictionary<string, string?>.Empty.Add("firstThird", "Ctrl+Alt+Left") };
        var error = Assert.Throws<ArgumentException>(settings.Validate);
        Assert.Contains("Left Half", error.Message);
        Assert.Contains("First Third", error.Message);
    }

    [Fact]
    public void LegacyJsonLoadsDefaultsAndSavedMapRoundTripsDeterministically()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PaneShift-test-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{\"gapPixels\":12,\"repeatedCommands\":{\"halfActions\":\"cycleSizes\"}}");
            var store = new SettingsStore(path);
            var legacy = store.LoadCandidate().Settings!;
            Assert.Equal(PaneShiftConfiguration.Default.Hotkeys.OrderBy(b => b.Action), HotkeySettings.Resolve(legacy).OrderBy(b => b.Action));
            var changed = legacy with { Hotkeys = ImmutableSortedDictionary<string, string?>.Empty.Add("leftHalf", "alt+ctrl+x").Add("center", null) };
            store.Save(changed);
            string first = File.ReadAllText(path);
            var loaded = store.LoadCandidate().Settings!;
            Assert.Equal("Ctrl+Alt+X", loaded.Hotkeys!["leftHalf"]);
            Assert.Null(loaded.Hotkeys["center"]);
            Assert.Equal(23, loaded.Hotkeys.Count);
            Assert.Equal(12, loaded.GapPixels);
            store.Save(loaded);
            Assert.Equal(first, File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally { Directory.Delete(directory, true); }
    }
}
