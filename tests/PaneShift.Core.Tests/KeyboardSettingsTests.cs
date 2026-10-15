using PaneShift.Core;

namespace PaneShift.Core.Tests;

public sealed class KeyboardSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PaneShift.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"keyboard\":{}}")]
    [InlineData("{\"gapPixels\":8,\"macStyleShortcuts\":{\"enabled\":true}}")]
    public void OlderSettingsDefaultOffWithoutRewritingAndRoundTripIndependently(string json)
    {
        Directory.CreateDirectory(directory);
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        File.WriteAllText(store.FilePath, json);
        var old = store.LoadCandidate().Settings!;
        Assert.False(old.Keyboard.RightAltGAtSign);
        Assert.Equal(json, File.ReadAllText(store.FilePath));
        store.Save(old with { Keyboard = new() { RightAltGAtSign = true } });
        var restored = store.LoadCandidate().Settings!;
        Assert.True(restored.Keyboard.RightAltGAtSign);
        Assert.Equal(old.GapPixels, restored.GapPixels);
        Assert.Equal(old.MacStyleShortcuts, restored.MacStyleShortcuts);
        Assert.Equal(HotkeySettings.Resolve(old), HotkeySettings.Resolve(restored));
    }

    [Theory]
    [InlineData("null")][InlineData("true")][InlineData("{\"rightAltGAtSign\":\"yes\"}")]
    public void InvalidKeyboardSectionIsLeftUntouched(string section)
    {
        Directory.CreateDirectory(directory);
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        string json = "{\"keyboard\":" + section + "}";
        File.WriteAllText(store.FilePath, json);
        Assert.Null(store.LoadCandidate().Settings);
        Assert.False(store.LoadOrCreate().Settings.Keyboard.RightAltGAtSign);
        Assert.Equal(json, File.ReadAllText(store.FilePath));
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
