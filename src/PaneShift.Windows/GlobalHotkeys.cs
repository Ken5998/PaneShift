using System.ComponentModel;
using System.Runtime.InteropServices;
using PaneShift.Core;

namespace PaneShift.Windows;

public sealed record HotkeyRegistrationFailure(HotkeyBinding Binding, string Reason);

/// <summary>Owned and disposed on the message-window thread.</summary>
public sealed class GlobalHotkeys(nint messageWindow) : IDisposable
{
    public const int HotkeyMessage = 0x0312;
    private readonly Dictionary<int, WindowAction> actions = [];
    private bool disposed;

    public IReadOnlyList<HotkeyRegistrationFailure> Register(IReadOnlyList<HotkeyBinding> bindings)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (actions.Count != 0) throw new InvalidOperationException("Hotkeys are already registered.");
        if (bindings.Count > 0xBFFF) throw new ArgumentOutOfRangeException(nameof(bindings));
        var failures = new List<HotkeyRegistrationFailure>();
        for (int i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            int id = i + 1;
            if (NativeMethods.RegisterHotKey(messageWindow, id,
                (uint)binding.Modifiers | NativeMethods.NoRepeat, binding.VirtualKey))
                actions.Add(id, binding.Action);
            else
                failures.Add(new(binding, new Win32Exception(Marshal.GetLastWin32Error()).Message));
        }
        return failures;
    }

    public bool TryGetAction(int id, out WindowAction action) => actions.TryGetValue(id, out action);

    public void Dispose()
    {
        if (disposed) return;
        foreach (int id in actions.Keys)
            if (!NativeMethods.UnregisterHotKey(messageWindow, id))
                System.Diagnostics.Trace.TraceError("UnregisterHotKey({0}): {1}", id, Marshal.GetLastWin32Error());
        actions.Clear();
        disposed = true;
    }
}
