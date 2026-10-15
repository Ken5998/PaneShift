using PaneShift.Core;

namespace PaneShift.Windows;

/// <summary>One immutable snapshot shared by native registration and the optional input filter.</summary>
public sealed class KeyboardShortcutConfiguration
{
    public static KeyboardShortcutConfiguration Disabled { get; } = From(new PaneShiftSettings());
    public bool MacStyleEnabled { get; }
    public bool RightAltGAtSign { get; }
    public bool NeedsHook => MacStyleEnabled || RightAltGAtSign;
    public IReadOnlyList<HotkeyBinding> NativeBindings { get; }
    public IReadOnlyList<HotkeyBinding> HookWindowBindings { get; }

    private KeyboardShortcutConfiguration(PaneShiftSettings settings)
    {
        MacStyleEnabled = settings.MacStyleShortcuts.Enabled;
        RightAltGAtSign = settings.Keyboard.RightAltGAtSign;
        var all = HotkeySettings.Resolve(settings);
        HookWindowBindings = Array.AsReadOnly(all.Where(b => RightAltGAtSign && UsesControlAlt(b)).ToArray());
        NativeBindings = Array.AsReadOnly(all.Where(b => !RightAltGAtSign || !UsesControlAlt(b)).ToArray());
    }

    public static KeyboardShortcutConfiguration From(PaneShiftSettings settings) => new(settings);
    private static bool UsesControlAlt(HotkeyBinding binding) =>
        (binding.Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt)) ==
        (HotkeyModifiers.Control | HotkeyModifiers.Alt);
}
