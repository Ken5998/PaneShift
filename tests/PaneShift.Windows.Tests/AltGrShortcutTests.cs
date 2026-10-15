using PaneShift.Core;
using PaneShift.App.Settings;

namespace PaneShift.Windows.Tests;

public sealed class AltGrShortcutTests
{
    private static KeyboardShortcutConfiguration Configuration(bool enabled = true, bool mac = false) =>
        KeyboardShortcutConfiguration.From(new() { Keyboard = new() { RightAltGAtSign = enabled }, MacStyleShortcuts = new() { Enabled = mac } });
    private const HotkeyModifiers ControlAlt = HotkeyModifiers.Control | HotkeyModifiers.Alt;

    [Fact]
    public void OptionalRoutingKeepsAllBindingsAndRestoresNativeRegistrationWhenDisabled()
    {
        var normal = Configuration(false);
        Assert.False(normal.NeedsHook);
        Assert.Equal(18, normal.NativeBindings.Count);
        Assert.Empty(normal.HookWindowBindings);
        var guarded = Configuration();
        Assert.True(guarded.NeedsHook);
        Assert.Equal(2, guarded.NativeBindings.Count);
        Assert.Equal(16, guarded.HookWindowBindings.Count);
        Assert.Equal(normal.NativeBindings.OrderBy(b => b.Action), guarded.NativeBindings.Concat(guarded.HookWindowBindings).OrderBy(b => b.Action));
    }

    [Theory]
    [InlineData(HotkeyModifiers.Alt)] // Right Alt without the synthetic Ctrl on another layout.
    [InlineData(ControlAlt)]
    public void RightAltGTypesExactlyOnceAndConsumesReleaseAfterModifiers(HotkeyModifiers modifiers)
    {
        var filter = new AltGrShortcutFilter(); int atSigns = 0;
        bool Process(bool up, HotkeyModifiers mods, bool rightAlt = true) =>
            filter.Process('G', up, false, mods, rightAlt, false, false, Configuration(), () => { atSigns++; return true; }, _ => throw new Exception());
        Assert.True(Process(false, modifiers));
        Assert.True(Process(false, modifiers));
        Assert.True(Process(true, HotkeyModifiers.None, false));
        Assert.False(Process(true, HotkeyModifiers.None, false));
        Assert.Equal(1, atSigns);
    }

    [Fact]
    public void GenuineControlLeftAltWindowShortcutsStillExecuteAllConfiguredActions()
    {
        var config = Configuration(); var filter = new AltGrShortcutFilter(); var actions = new List<HotkeyBinding>();
        foreach (var binding in config.HookWindowBindings)
        {
            Assert.False(filter.Process(binding.VirtualKey, false, true, binding.Modifiers, false, true, false, config,
                () => throw new Exception(), _ => throw new Exception()));
            Assert.True(filter.Process(binding.VirtualKey, false, false, binding.Modifiers, false, true, false, config,
                () => throw new Exception(), actions.Add));
            Assert.True(filter.Process(binding.VirtualKey, false, false, binding.Modifiers, false, true, false, config,
                () => throw new Exception(), actions.Add));
            Assert.True(filter.Process(binding.VirtualKey, true, false, HotkeyModifiers.None, false, false, false, config,
                () => throw new Exception(), actions.Add));
        }
        Assert.Equal(config.HookWindowBindings, actions);
    }

    [Fact]
    public void NativeAltGrIncludingOtherRegisteredKeysIsPassedThroughWithoutTranslation()
    {
        var config = Configuration(); var filter = new AltGrShortcutFilter();
        // E/R/T and the other default Ctrl+Alt bindings must not steal €, dead keys, etc.
        foreach (uint key in Enumerable.Range(1, 255).Select(k => (uint)k).Where(k => k != 'G'))
        foreach (var modifiers in new[] { ControlAlt, ControlAlt | HotkeyModifiers.Shift })
        foreach (bool up in new[] { false, true })
            Assert.False(filter.Process(key, up, false, modifiers, true, false, false, config,
                () => throw new Exception(), _ => throw new Exception()));
    }

    [Theory]
    [InlineData(false, false, false, false, false, ControlAlt)] // Disabled.
    [InlineData(true, true, false, false, false, ControlAlt)] // Injected.
    [InlineData(true, false, true, false, false, ControlAlt)] // G already held.
    [InlineData(true, false, false, true, false, ControlAlt)] // Both Alt keys.
    [InlineData(true, false, false, false, true, ControlAlt)] // Explicit Right Ctrl.
    [InlineData(true, false, false, false, false, ControlAlt | HotkeyModifiers.Shift)]
    [InlineData(true, false, false, false, false, ControlAlt | HotkeyModifiers.Windows)]
    public void DisabledInjectedExtraModifiersAndPreheldKeysNeverTranslate(bool enabled, bool injected,
        bool alreadyDown, bool leftAlt, bool rightControl, HotkeyModifiers modifiers)
    {
        Assert.False(new AltGrShortcutFilter().Process('G', false, injected, modifiers, true, leftAlt, rightControl,
            Configuration(enabled), () => throw new Exception(), _ => throw new Exception(), alreadyDown));
    }

    [Fact]
    public void FailedAtSignStillSuppressesConflictingWindowAction()
    {
        var filter = new AltGrShortcutFilter();
        Assert.True(filter.Process('G', false, false, ControlAlt, true, false, false, Configuration(), () => false, _ => throw new Exception()));
        Assert.True(filter.Process('G', true, false, HotkeyModifiers.None, false, false, false, Configuration(), () => throw new Exception(), _ => throw new Exception()));
    }

    [Fact]
    public void DraftApplyCancelDefaultsAndReloadRemainIndependentOfMacStyleFlag()
    {
        PaneShiftSettings active = new();
        var model = new SettingsViewModel(active, s => { active = s; return null; });
        model.RightAltGAtSign = true;
        Assert.True(model.CanApply);
        Assert.False(active.Keyboard.RightAltGAtSign);
        model.Load(active);
        Assert.False(model.RightAltGAtSign);
        model.RightAltGAtSign = true;
        Assert.True(model.Apply());
        Assert.True(active.Keyboard.RightAltGAtSign);
        Assert.False(active.MacStyleShortcuts.Enabled);
        model.RestoreDefaults();
        Assert.False(model.RightAltGAtSign);
        Assert.True(active.Keyboard.RightAltGAtSign);
        Assert.True(model.Apply());
        Assert.False(active.Keyboard.RightAltGAtSign);
        model.ExternalReload(new() { Keyboard = new() { RightAltGAtSign = true } });
        Assert.True(model.RightAltGAtSign);
        Assert.False(model.HasChanges);
    }
}
