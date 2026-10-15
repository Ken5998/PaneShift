using System.Collections.Immutable;
using PaneShift.Core;
using PaneShift.App.Settings;

namespace PaneShift.Windows.Tests;

public sealed class MacStyleActivationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "PaneShift.Tests", Guid.NewGuid().ToString("N"));
    private sealed class Layer : IMacStyleShortcutService
    {
        public bool Active, Prepared, Fail;
        public KeyboardShortcutConfiguration Configuration = KeyboardShortcutConfiguration.Disabled;
        public string? Prepare(KeyboardShortcutConfiguration configuration) { Prepared = configuration.NeedsHook; return Fail && Prepared ? "Hook unavailable" : null; }
        public void Commit(KeyboardShortcutConfiguration configuration) { Configuration = configuration; Active = configuration.NeedsHook; Prepared = false; }
        public void Rollback() => Prepared = false;
    }
    private sealed class Backend : IHotkeyBackend
    {
        public Dictionary<int, HotkeyChord> Held { get; } = [];
        public HotkeyChord? Conflict;
        public string? Register(int id, HotkeyChord chord)
        {
            if (chord == Conflict) return "Conflict";
            Held.Add(id, chord); return null;
        }
        public string? Unregister(int id) { Held.Remove(id); return null; }
    }

    [Fact]
    public void EnableDisableReenableAndPausePreserveWindowBindings()
    {
        var runtime = new RuntimeSettings(); var backend = new Backend(); var layer = new Layer();
        using var hotkeys = new GlobalHotkeys(backend);
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        var activation = new ConfigurationActivation(runtime, store, hotkeys, layer);
        foreach (bool enabled in new[] { false, true, false, true })
        {
            var candidate = new PaneShiftSettings { MacStyleShortcuts = new() { Enabled = enabled } };
            Assert.Null(activation.Apply(candidate, true, false));
            Assert.Equal(enabled, layer.Active);
            Assert.False(layer.Prepared);
            Assert.Equal(18, backend.Held.Count);
            foreach (int id in backend.Held.Keys) Assert.True(hotkeys.TryGetAction(id, out _));
        }
        Assert.Null(activation.Apply(runtime.Current, false, true));
        Assert.False(layer.Active);
        Assert.Empty(backend.Held);
        Assert.True(runtime.Current.MacStyleShortcuts.Enabled);
        Assert.Null(activation.Reload(false));
        Assert.True(layer.Active);
    }

    [Theory]
    [InlineData("hook", false)][InlineData("registration", false)][InlineData("disk", false)]
    [InlineData("registration", true)][InlineData("disk", true)]
    public void FailureRestoresAllLayersAndFile(string failure, bool previouslyEnabled)
    {
        var runtime = new RuntimeSettings();
        runtime.ReplaceCurrent(new() { MacStyleShortcuts = new() { Enabled = previouslyEnabled } });
        var layer = new Layer { Active = previouslyEnabled }; var backend = new Backend();
        using var hotkeys = new GlobalHotkeys(backend);
        hotkeys.Register(HotkeySettings.Resolve(runtime.Current));
        var before = backend.Held.ToArray();
        var store = new SettingsStore(Path.Combine(directory, "settings.json")); store.Save(runtime.Current);
        string original = File.ReadAllText(store.FilePath);
        if (failure == "hook") layer.Fail = true;
        if (failure == "registration") backend.Conflict = HotkeyChord.Parse("Ctrl+Alt+B");
        var activation = new ConfigurationActivation(runtime, failure == "disk" ? new SettingsStore(directory) : store, hotkeys, layer);
        var candidate = new PaneShiftSettings { MacStyleShortcuts = new() { Enabled = !previouslyEnabled },
            Hotkeys = ImmutableSortedDictionary<string, string?>.Empty.Add("center", "Ctrl+Alt+B") };
        Assert.NotNull(activation.Apply(candidate, true, false));
        Assert.Equal(previouslyEnabled, layer.Active);
        Assert.Equal(previouslyEnabled, runtime.Current.MacStyleShortcuts.Enabled);
        Assert.False(layer.Prepared);
        Assert.Equal(before, backend.Held.ToArray());
        Assert.Equal(original, File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void DraftApplyCancelResetAndExternalReloadUseExistingSemantics()
    {
        PaneShiftSettings active = new();
        var model = new SettingsViewModel(active, s => { active = s; return null; });
        model.MacStyleEnabled = true;
        Assert.False(active.MacStyleShortcuts.Enabled);
        Assert.True(model.CanApply);
        model.Load(active); // Cancel/discard draft.
        Assert.False(model.MacStyleEnabled);
        model.MacStyleEnabled = true; Assert.True(model.Apply());
        Assert.True(active.MacStyleShortcuts.Enabled);
        model.RestoreDefaults();
        Assert.True(active.MacStyleShortcuts.Enabled);
        Assert.False(model.MacStyleEnabled);
        Assert.True(model.Apply());
        Assert.False(active.MacStyleShortcuts.Enabled);
        model.ExternalReload(new() { MacStyleShortcuts = new() { Enabled = true } });
        Assert.True(model.MacStyleEnabled);
        Assert.False(model.HasChanges);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void AtSignEnableDisableReenableReloadAndPauseAreTransactional(bool macEnabled)
    {
        var runtime = new RuntimeSettings(); var backend = new Backend(); var layer = new Layer();
        using var hotkeys = new GlobalHotkeys(backend);
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        var activation = new ConfigurationActivation(runtime, store, hotkeys, layer);
        foreach (bool atSign in new[] { false, true, false, true })
        {
            var candidate = new PaneShiftSettings { Keyboard = new() { RightAltGAtSign = atSign }, MacStyleShortcuts = new() { Enabled = macEnabled } };
            Assert.Null(activation.Apply(candidate, true, false));
            Assert.Equal(atSign, layer.Configuration.RightAltGAtSign);
            Assert.Equal(macEnabled, layer.Configuration.MacStyleEnabled);
            Assert.Equal(atSign ? 2 : 18, backend.Held.Count);
            Assert.Equal(18, backend.Held.Count + layer.Configuration.HookWindowBindings.Count);
            Assert.True(store.LoadCandidate().Settings!.Keyboard.RightAltGAtSign == atSign);
        }
        Assert.Null(activation.Apply(runtime.Current, false, true));
        Assert.False(layer.Active); Assert.Empty(backend.Held);
        Assert.True(runtime.Current.Keyboard.RightAltGAtSign);
        Assert.Null(activation.Reload(false));
        Assert.True(layer.Configuration.RightAltGAtSign);
        Assert.Equal(macEnabled, layer.Configuration.MacStyleEnabled);
        Assert.Equal(2, backend.Held.Count);
    }

    [Theory]
    [InlineData("hook", false)][InlineData("registration", false)][InlineData("disk", false)]
    [InlineData("registration", true)][InlineData("disk", true)]
    public void AtSignFailureRetainsPreviousRoutingHookAndFile(string failure, bool previous)
    {
        var runtime = new RuntimeSettings(); var backend = new Backend(); var layer = new Layer();
        using var hotkeys = new GlobalHotkeys(backend);
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        var activation = new ConfigurationActivation(runtime, store, hotkeys, layer);
        Assert.Null(activation.Apply(new() { Keyboard = new() { RightAltGAtSign = previous } }, true, false));
        var before = backend.Held.ToArray();
        var oldConfiguration = layer.Configuration;
        string original = File.ReadAllText(store.FilePath);
        if (failure == "hook") layer.Fail = true;
        if (failure == "registration") backend.Conflict = HotkeyChord.Parse("Ctrl+Win+B");
        if (failure == "disk") activation = new(runtime, new SettingsStore(directory), hotkeys, layer);
        var candidate = new PaneShiftSettings { Keyboard = new() { RightAltGAtSign = !previous },
            Hotkeys = ImmutableSortedDictionary<string, string?>.Empty.Add("center", "Ctrl+Win+B") };
        Assert.NotNull(activation.Apply(candidate, true, false));
        Assert.Equal(previous, runtime.Current.Keyboard.RightAltGAtSign);
        Assert.Same(oldConfiguration, layer.Configuration);
        Assert.Equal(before, backend.Held.ToArray());
        Assert.Equal(original, File.ReadAllText(store.FilePath));
        Assert.False(layer.Prepared);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
