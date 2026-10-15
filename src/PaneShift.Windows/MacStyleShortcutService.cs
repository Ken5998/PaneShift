using System.ComponentModel;
using System.Runtime.InteropServices;
using PaneShift.Core;

namespace PaneShift.Windows;

/// <summary>Optional exact-whitelist hook, on its own message thread so WPF cannot time it out.</summary>
public sealed class MacStyleShortcutService(nint statusWindow) : IMacStyleShortcutService, IDisposable
{
    public const int InputFailedMessage = 0x8001;
    public const int WindowShortcutMessage = 0x8002;
    private HookSession? session;
    private bool enabled;
    private int generation;

    public string? Prepare(KeyboardShortcutConfiguration configuration)
    {
        if (!configuration.NeedsHook || session is not null) return null;
        session = new HookSession(statusWindow);
        if (session.StartError is not { } error) return null;
        session.Dispose(); session = null;
        return $"Keyboard shortcuts could not be enabled. {error}";
    }

    public void Commit(KeyboardShortcutConfiguration configuration)
    {
        enabled = configuration.NeedsHook;
        if (session is not null) session.State = new(configuration, Interlocked.Increment(ref generation));
        if (!enabled) Rollback();
    }

    public bool TryGetWindowBinding(nint action, nint token, out HotkeyBinding? binding)
    {
        var state = session?.State;
        binding = state is not null && state.Generation == token && state.Configuration.RightAltGAtSign
            ? state.Configuration.HookWindowBindings.FirstOrDefault(b => (nint)b.Action == action) : null;
        return binding is not null;
    }

    public void Rollback()
    {
        if (enabled) return;
        session?.Dispose(); session = null;
    }

    public void Dispose() => Commit(KeyboardShortcutConfiguration.Disabled);

    private sealed record HookState(KeyboardShortcutConfiguration Configuration, int Generation);

    private sealed class HookSession : IDisposable
    {
        private readonly Thread thread;
        private readonly ManualResetEventSlim ready = new();
        private readonly nint statusWindow;
        private readonly ShortcutTranslationFilter filter = new();
        private readonly AltGrShortcutFilter altGr = new();
        private readonly NativeInput backend = new();
        private readonly ShortcutInputSender sender;
        private readonly KeyboardNative.HookProc callback;
        private uint threadId;
        private nint hook;
        public volatile HookState State = new(KeyboardShortcutConfiguration.Disabled, 0);
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
            var state = State;
            if (code >= 0 && state.Configuration.NeedsHook)
            {
                var input = Marshal.PtrToStructure<KeyboardNative.KeyboardEvent>(data);
                if ((input.Flags & 0x12) == 0) // Ignore all injected input, including our tagged SendInput.
                {
                    var modifiers = HotkeyModifiers.None;
                    if (backend.IsDown(0x5B) || backend.IsDown(0x5C)) modifiers |= HotkeyModifiers.Windows;
                    if (backend.IsDown(0x10)) modifiers |= HotkeyModifiers.Shift;
                    if (backend.IsDown(0x11)) modifiers |= HotkeyModifiers.Control;
                    if (backend.IsDown(0x12)) modifiers |= HotkeyModifiers.Alt;
                    bool up = (input.Flags & 0x80) != 0;
                    bool down = backend.IsDown((ushort)input.Key);
                    if (altGr.Process(input.Key, up, false, modifiers, backend.IsDown(0xA5), backend.IsDown(0xA4),
                        backend.IsDown(0xA3), state.Configuration,
                        () => Report(sender.SendAtSign()),
                        binding => PostWindowCommand(binding, state), down)) return 1;
                    if (filter.Process(input.Key, up, false, modifiers, Send, down,
                        state.Configuration.MacStyleEnabled)) return 1;
                }
            }
            return KeyboardNative.CallNextHookEx(0, code, message, data);
        }

        private bool Send(ShortcutTranslation translation)
        {
            return Report(sender.Send(translation));
        }

        private void PostWindowCommand(HotkeyBinding binding, HookState state)
        {
            // The source key is consumed, so suppress a bare Alt/Win menu gesture too.
            sender.MaskMenu();
            KeyboardNative.PostMessage(statusWindow, WindowShortcutMessage, (nuint)binding.Action, state.Generation);
        }

        private bool Report(bool success)
        {
            if (!success) KeyboardNative.PostMessage(statusWindow, InputFailedMessage, 0, 0);
            return success;
        }

        public void Dispose()
        {
            State = new(KeyboardShortcutConfiguration.Disabled, 0);
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
                    Key = i.Unicode ? (ushort)0 : i.Key, Scan = i.Unicode ? i.Key : (ushort)0,
                    Flags = (i.Up ? 2u : 0u) | (i.Unicode ? 4u : i.Key is 0x5B or 0x5C or 0xA3 or 0xA5 ? 1u : 0u),
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
