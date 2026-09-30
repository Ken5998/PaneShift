using System.Collections.Immutable;
using PaneShift.Core;

namespace PaneShift.Core.Tests;

public sealed class MacStyleShortcutTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PaneShift.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("Win+C", "Ctrl+C")]
    [InlineData("Win+V", "Ctrl+V")]
    [InlineData("Win+X", "Ctrl+X")]
    [InlineData("Win+A", "Ctrl+A")]
    [InlineData("Win+Z", "Ctrl+Z")]
    [InlineData("Win+Shift+Z", "Ctrl+Shift+Z")]
    [InlineData("Win+S", "Ctrl+S")]
    [InlineData("Win+F", "Ctrl+F")]
    [InlineData("Win+T", "Ctrl+T")]
    [InlineData("Win+Shift+T", "Ctrl+Shift+T")]
    [InlineData("Win+W", "Ctrl+W")]
    [InlineData("Win+Q", "Alt+F4")]
    public void ExactMapping(string source, string target)
    {
        Assert.Equal(HotkeyChord.Parse(target), MacStyleShortcuts.Find(HotkeyChord.Parse(source))!.Target);
        Assert.Equal(12, MacStyleShortcuts.All.Count);
    }

    [Theory]
    [InlineData("Win+E")][InlineData("Win+R")][InlineData("Win+L")][InlineData("Win+D")]
    [InlineData("Win+Tab")][InlineData("Win+1")][InlineData("Win+Left")]
    [InlineData("Win+Shift+W")][InlineData("Win+Ctrl+C")][InlineData("Win+Alt+Q")]
    public void UnlistedChordNeverMatches(string chord) => Assert.Null(MacStyleShortcuts.Find(HotkeyChord.Parse(chord)));

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"gapPixels\":8,\"hotkeys\":{\"center\":null}}")]
    public void OldConfigurationDefaultsOffAndSurvivesRoundTrip(string json)
    {
        Directory.CreateDirectory(directory);
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        File.WriteAllText(store.FilePath, json);
        var old = store.LoadCandidate().Settings!;
        Assert.False(old.MacStyleShortcuts.Enabled);
        store.Save(old with { MacStyleShortcuts = new() { Enabled = true } });
        var restored = store.LoadCandidate().Settings!;
        Assert.True(restored.MacStyleShortcuts.Enabled);
        Assert.Equal(old.GapPixels, restored.GapPixels);
        Assert.Equal(HotkeySettings.Resolve(old), HotkeySettings.Resolve(restored));
    }

    [Theory]
    [InlineData("null")][InlineData("true")][InlineData("{\"enabled\":\"yes\"}")]
    public void InvalidSectionFailsSafely(string section)
    {
        Directory.CreateDirectory(directory);
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        string json = "{\"macStyleShortcuts\":" + section + "}";
        File.WriteAllText(store.FilePath, json);
        Assert.Null(store.LoadCandidate().Settings);
        Assert.False(store.LoadOrCreate().Settings.MacStyleShortcuts.Enabled);
        Assert.Equal(json, File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void WindowBindingConflictRejectedOnlyWhenEnabled()
    {
        var settings = new PaneShiftSettings { Hotkeys = ImmutableSortedDictionary<string, string?>.Empty.Add("center", "Win+W") };
        settings.Validate();
        Assert.Contains("reserved", Assert.Throws<ArgumentException>(() =>
            (settings with { MacStyleShortcuts = new() { Enabled = true } }).Validate()).Message);
        Assert.False(new PaneShiftSettings().MacStyleShortcuts.Enabled);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
