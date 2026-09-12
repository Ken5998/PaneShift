using System.Collections.Immutable;
using PaneShift.Core;

namespace PaneShift.Windows.Tests;

public sealed class ConfigurationActivationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PaneShift.Tests", Guid.NewGuid().ToString("N"));
    private sealed class Backend : IHotkeyBackend
    {
        public Dictionary<int, HotkeyChord> Held { get; } = [];
        public HotkeyChord? Conflict { get; set; }
        public string? Register(int id, HotkeyChord chord)
        {
            if (Conflict == chord) return "Reserved by another application";
            Assert.DoesNotContain(chord, Held.Values);
            Held.Add(id, chord);
            return null;
        }
        public string? Unregister(int id) { Assert.True(Held.Remove(id)); return null; }
    }

    [Fact]
    public void RegistrationConflictLeavesOldMapRuntimeAndFileUntouched()
    {
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        var runtime = new RuntimeSettings();
        store.Save(runtime.Current);
        string original = File.ReadAllText(store.FilePath);
        var backend = new Backend();
        using var hotkeys = new GlobalHotkeys(backend);
        Assert.Empty(hotkeys.Register(HotkeySettings.Resolve(runtime.Current)));
        var held = backend.Held.ToArray();
        var active = runtime.Current;
        backend.Conflict = HotkeyChord.Parse("Ctrl+Alt+X");
        var draft = active with { GapPixels = 12, Hotkeys = ImmutableSortedDictionary<string, string?>.Empty
            .Add("leftHalf", "Ctrl+Alt+Z").Add("rightHalf", "Ctrl+Alt+X") };
        var error = new ConfigurationActivation(runtime, store, hotkeys).Apply(draft, true, false);
        Assert.Contains("Right Half", error);
        Assert.Same(active, runtime.Current);
        Assert.Equal(original, File.ReadAllText(store.FilePath));
        Assert.Equal(held, backend.Held.ToArray());
    }

    [Fact]
    public void DiskFailureRemovesOnlyPreparedHotkeys()
    {
        var backend = new Backend();
        using var hotkeys = new GlobalHotkeys(backend);
        var runtime = new RuntimeSettings();
        hotkeys.Register(HotkeySettings.Resolve(runtime.Current));
        var before = backend.Held.ToArray();
        Directory.CreateDirectory(directory);
        var store = new SettingsStore(directory); // Cannot replace a directory with a settings file.
        var candidate = new PaneShiftSettings { Hotkeys = ImmutableSortedDictionary<string, string?>.Empty.Add("leftHalf", "Ctrl+Alt+Z") };
        Assert.NotNull(new ConfigurationActivation(runtime, store, hotkeys).Apply(candidate, true, false));
        Assert.Equal(before, backend.Held.ToArray());
        Assert.Null(runtime.Current.Hotkeys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessSupportsSwapsDisablingAndPause(bool paused)
    {
        var backend = new Backend();
        using var hotkeys = new GlobalHotkeys(backend);
        var runtime = new RuntimeSettings();
        if (!paused) hotkeys.Register(HotkeySettings.Resolve(runtime.Current));
        var candidate = new PaneShiftSettings { GapPixels = 12, Hotkeys = ImmutableSortedDictionary<string, string?>.Empty
            .Add("leftHalf", "Ctrl+Alt+Right").Add("rightHalf", "Ctrl+Alt+Left").Add("center", null) };
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        Assert.Null(new ConfigurationActivation(runtime, store, hotkeys).Apply(candidate, true, paused));
        Assert.Same(candidate, runtime.Current);
        Assert.Equal(12, store.LoadCandidate().Settings!.GapPixels);
        if (paused) Assert.Empty(backend.Held);
        else
        {
            Assert.Equal(17, backend.Held.Count);
            int id = backend.Held.Single(p => p.Value == HotkeyChord.Parse("Ctrl+Alt+Right")).Key;
            Assert.True(hotkeys.TryGetAction(id, out var action));
            Assert.Equal(WindowAction.LeftHalf, action);
        }
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
