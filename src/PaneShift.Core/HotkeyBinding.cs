namespace PaneShift.Core;

[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Control = 2, Shift = 4, Windows = 8 }

public sealed record HotkeyBinding(HotkeyModifiers Modifiers, uint VirtualKey, WindowAction Action);

public sealed record PaneShiftConfiguration(IReadOnlyList<HotkeyBinding> Hotkeys)
{
    public static PaneShiftConfiguration Default { get; } = new(Array.AsReadOnly(new[]
    {
        Bind(0x25, WindowAction.LeftHalf), Bind(0x27, WindowAction.RightHalf),
        Bind(0x26, WindowAction.TopHalf), Bind(0x28, WindowAction.BottomHalf),
        Bind('U', WindowAction.TopLeft), Bind('I', WindowAction.TopRight),
        Bind('J', WindowAction.BottomLeft), Bind('K', WindowAction.BottomRight),
        Bind('D', WindowAction.FirstThird), Bind('F', WindowAction.CenterThird),
        Bind('G', WindowAction.LastThird), Bind('E', WindowAction.FirstTwoThirds),
        Bind('R', WindowAction.CenterTwoThirds), Bind('T', WindowAction.LastTwoThirds),
        Bind(0x0D, WindowAction.Maximize), Bind('C', WindowAction.Center),
        new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows,
            0x26, WindowAction.TopRightSixth),
        new HotkeyBinding(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows,
            0x28, WindowAction.BottomRightSixth)
    }));

    private static HotkeyBinding Bind(uint key, WindowAction action) =>
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, key, action);
}
