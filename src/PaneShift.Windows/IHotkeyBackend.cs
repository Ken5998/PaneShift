using System.ComponentModel;
using System.Runtime.InteropServices;
using PaneShift.Core;

namespace PaneShift.Windows;

public interface IHotkeyBackend
{
    string? Register(int id, HotkeyChord chord);
    string? Unregister(int id);
}

internal sealed class NativeHotkeyBackend(nint window) : IHotkeyBackend
{
    public string? Register(int id, HotkeyChord chord) =>
        NativeMethods.RegisterHotKey(window, id, (uint)chord.Modifiers | NativeMethods.NoRepeat, chord.VirtualKey)
            ? null : new Win32Exception(Marshal.GetLastWin32Error()).Message;
    public string? Unregister(int id) => NativeMethods.UnregisterHotKey(window, id)
        ? null : new Win32Exception(Marshal.GetLastWin32Error()).Message;
}
