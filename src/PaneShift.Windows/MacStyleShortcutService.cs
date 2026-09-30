using System.ComponentModel;
using System.Runtime.InteropServices;
using PaneShift.Core;

namespace PaneShift.Windows;

/// <summary>Optional exact-whitelist hook, on its own message thread so WPF cannot time it out.</summary>
public sealed class MacStyleShortcutService(nint statusWindow) : IMacStyleShortcutService, IDisposable
{
    public const int InputFailedMessage = 0x8001;
    private HookSession? session;
    private bool enabled;

    public string? Prepare(bool requested)
    {
        if (!requested || session is not null) return null;
        session = new HookSession(statusWindow);
        if (session.StartError is not { } error) return null;
        session.Dispose(); session = null;
        return $"Mac-style shortcuts could not be enabled. {error}";
    }

    public void Commit(bool requested)
    {
        enabled = requested;
        if (session is not null) session.Active = requested;
        if (!requested) Rollback();
    }

    public void Rollback()
    {
        if (enabled) return;
        session?.Dispose(); session = null;
    }

    public void Dispose() => Commit(false);

    private sealed class HookSession : IDisposable
    {
        private readonly Thread thread;
        private readonly ManualResetEventSlim ready = new();
        private readonly nint statusWindow;
        private readonly ShortcutTranslationFilter filter = new();
        private readonly NativeInput backend = new();
        private readonly ShortcutInputSender sender;
        private readonly KeyboardNative.HookProc callback;
        private uint threadId;
        private nint hook;
        public volatile bool Active;
        public string? StartError { get; private set; }

        public HookSession(nint statusWindow)
        {
            this.statusWindow = statusWindow;
            sender = new(backend);
            callback = OnKeyboard;
            thread = new Thread(Run) { IsBackground = true, Name = "PaneShift shortcut translation" };
            thread.Start();
            ready.Wait();
        }

        private void Run()
        {
            threadId = KeyboardNative.GetCurrentThreadId();
            KeyboardNative.PeekMessage(out _, 0, 0, 0, 0); // Create queue before announcing readiness.
            hook = KeyboardNative.SetWindowsHookEx(13, callback, KeyboardNative.GetModuleHandle(null), 0);
            if (hook == 0) StartError = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            ready.Set();
            if (hook == 0) return;
            try
            {
                while (KeyboardNative.GetMessage(out var message, 0, 0, 0) > 0)
                {
                    KeyboardNative.TranslateMessage(in message);
                    KeyboardNative.DispatchMessage(in message);
                }
            }
            finally { KeyboardNative.UnhookWindowsHookEx(hook); }
        }

        private nint OnKeyboard(int code, nuint message, nint data)
        {
            if (code >= 0 && Active)
            {
                var input = Marshal.PtrToStructure<KeyboardNative.KeyboardEvent>(data);
                if ((input.Flags & 0x12) == 0) // Ignore all injected input, including our tagged SendInput.
                {
                    var modifiers = HotkeyModifiers.None;
                    if (backend.IsDown(0x5B) || backend.IsDown(0x5C)) modifiers |= HotkeyModifiers.Windows;
                    if (backend.IsDown(0x10)) modifiers |= HotkeyModifiers.Shift;
                    if (backend.IsDown(0x11)) modifiers |= HotkeyModifiers.Control;
                    if (backend.IsDown(0x12)) modifiers |= HotkeyModifiers.Alt;
                    if (filter.Process(input.Key, (input.Flags & 0x80) != 0, false, modifiers, Send,
                        backend.IsDown((ushort)input.Key))) return 1;
                }
            }
            return KeyboardNative.CallNextHookEx(0, code, message, data);
        }

        private bool Send(ShortcutTranslation translation)
        {
            bool success = sender.Send(translation);
            if (!success) KeyboardNative.PostMessage(statusWindow, InputFailedMessage, 0, 0);
            return success;
        }

        public void Dispose()
        {
            Active = false;
            if (thread.IsAlive)
            {
                KeyboardNative.PostThreadMessage(threadId, 0x0012, 0, 0);
                thread.Join();
            }
            ready.Dispose();
        }
    }

    private sealed class NativeInput : IShortcutInputBackend
    {
        public bool IsDown(ushort key) => (KeyboardNative.GetAsyncKeyState(key) & 0x8000) != 0;
        public int Send(IReadOnlyList<ShortcutKeyInput> inputs)
        {
            var native = inputs.Select(i => new KeyboardNative.Input
            {
                Type = 1,
                Data = new KeyboardNative.InputUnion { Keyboard = new()
                {
                    Key = i.Key, Flags = (i.Up ? 2u : 0u) | (i.Key is 0x5B or 0x5C ? 1u : 0u),
                    ExtraInfo = 0x504E5348
                } }
            }).ToArray();
            return (int)KeyboardNative.SendInput((uint)native.Length, native, Marshal.SizeOf<KeyboardNative.Input>());
        }
    }
}

internal static class KeyboardNative
{
    internal delegate nint HookProc(int code, nuint message, nint data);
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardEvent { public uint Key, Scan, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse; // Required native INPUT union size/alignment on both x86 and x64.
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Sequential)] internal struct Message { public nint Window; public uint Id; public nuint WParam; public nint LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] internal static extern nint SetWindowsHookEx(int type, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nuint message, nint data);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PeekMessage(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll", EntryPoint = "GetMessageW")] internal static extern int GetMessage(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool TranslateMessage(in Message message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] internal static extern nint DispatchMessage(in Message message);
    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostThreadMessage(uint thread, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
}
