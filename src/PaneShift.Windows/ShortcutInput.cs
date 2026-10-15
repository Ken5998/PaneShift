using PaneShift.Core;

namespace PaneShift.Windows;

public readonly record struct ShortcutKeyInput(ushort Key, bool Up = false, bool Unicode = false);

public interface IShortcutInputBackend
{
    bool IsDown(ushort key);
    int Send(IReadOnlyList<ShortcutKeyInput> inputs);
}

/// <summary>One balanced batch; physical Win/Shift are restored for the next native chord.</summary>
public sealed class ShortcutInputSender(IShortcutInputBackend backend)
{
    // Unassigned VK used solely to prevent releasing/restoring Win from opening Start.
    public const ushort MenuMaskKey = 0xE8;
    private static readonly ushort[] PhysicalModifiers = [0x5B, 0x5C, 0xA0, 0xA1];

    public bool Send(ShortcutTranslation translation)
    {
        ushort[] held = PhysicalModifiers.Where(backend.IsDown).ToArray();
        var inputs = CreateSequence(translation, held);
        return SendBatch(inputs, held);
    }

    public bool SendAtSign()
    {
        // VK_PACKET inserts Unicode text, not a Ctrl/Alt application shortcut. Never
        // release/repress AltGr: its companion Left Ctrl is managed by Windows, not
        // a physically held modifier that we may restore with a synthetic key-down.
        return SendBatch(CreateTextSequence('@'), []);
    }

    public bool MaskMenu() => SendBatch([new(MenuMaskKey), new(MenuMaskKey, true)], []);

    private bool SendBatch(IReadOnlyList<ShortcutKeyInput> inputs, ushort[] held)
    {
        int sent = backend.Send(inputs);
        if (sent == inputs.Count) return true;
        if (sent > 0)
        {
            // Recover only state changed by the accepted prefix, never leave a synthetic
            // Ctrl/Alt/key held after a partial SendInput result. UIPI can reject all input.
            var before = held.Select(k => (Key: k, Unicode: false)).ToHashSet();
            var after = before.ToHashSet();
            foreach (var input in inputs.Take(sent))
                if (input.Up) after.Remove((input.Key, input.Unicode)); else after.Add((input.Key, input.Unicode));
            var recovery = after.Except(before).Select(k => new ShortcutKeyInput(k.Key, true, k.Unicode))
                .Concat(before.Except(after).Select(k => new ShortcutKeyInput(k.Key))).ToList();
            recovery.Add(new(MenuMaskKey)); recovery.Add(new(MenuMaskKey, true));
            backend.Send(recovery);
        }
        return false;
    }

    public static IReadOnlyList<ShortcutKeyInput> CreateSequence(ShortcutTranslation translation, IReadOnlyList<ushort> held)
    {
        var inputs = new List<ShortcutKeyInput>(20) { new(MenuMaskKey), new(MenuMaskKey, true) };
        foreach (ushort key in held) inputs.Add(new(key, true));
        ushort modifier = translation.Target.Modifiers.HasFlag(HotkeyModifiers.Control) ? (ushort)0xA2 : (ushort)0xA4;
        bool shift = translation.Target.Modifiers.HasFlag(HotkeyModifiers.Shift);
        inputs.Add(new(modifier));
        if (shift) inputs.Add(new(0xA0));
        inputs.Add(new((ushort)translation.Target.VirtualKey));
        inputs.Add(new((ushort)translation.Target.VirtualKey, true));
        if (shift) inputs.Add(new(0xA0, true));
        inputs.Add(new(modifier, true));
        foreach (ushort key in held) inputs.Add(new(key));
        inputs.Add(new(MenuMaskKey)); inputs.Add(new(MenuMaskKey, true));
        return inputs;
    }

    public static IReadOnlyList<ShortcutKeyInput> CreateTextSequence(char character)
    {
        var inputs = new List<ShortcutKeyInput> { new(MenuMaskKey), new(MenuMaskKey, true) };
        inputs.Add(new(character, Unicode: true));
        inputs.Add(new(character, Up: true, Unicode: true));
        inputs.Add(new(MenuMaskKey)); inputs.Add(new(MenuMaskKey, true));
        return inputs;
    }
}

/// <summary>Stores only outstanding consumed whitelist keys, never typed text or activity.</summary>
public sealed class ShortcutTranslationFilter
{
    private readonly bool[] consumed = new bool[256];

    public bool Process(uint key, bool up, bool injected, HotkeyModifiers modifiers,
        Func<ShortcutTranslation, bool> send, bool alreadyDown = false, bool enabled = true)
    {
        if (injected || key >= consumed.Length) return false;
        if (consumed[key])
        {
            if (up) consumed[key] = false;
            return true; // No auto-repeat, and no orphan source key-up, even after Win is released.
        }
        // A key held before Win was pressed belongs to the application, including its key-up.
        if (!enabled || up || alreadyDown || MacStyleShortcuts.Find(new(modifiers, key)) is not { } translation) return false;
        consumed[key] = true;
        send(translation);
        return true; // A failed translation must not unexpectedly invoke the Windows source action.
    }
}

/// <summary>Only the opted-in at-sign chord and exact configured Ctrl+Alt window chords.</summary>
public sealed class AltGrShortcutFilter
{
    private readonly bool[] consumed = new bool[256];

    public bool Process(uint key, bool up, bool injected, HotkeyModifiers modifiers,
        bool rightAlt, bool leftAlt, bool rightControl, KeyboardShortcutConfiguration configuration,
        Func<bool> sendAtSign, Action<HotkeyBinding> windowCommand, bool alreadyDown = false)
    {
        if (injected || key >= consumed.Length) return false;
        if (consumed[key])
        {
            if (up) consumed[key] = false;
            return true;
        }
        if (!configuration.RightAltGAtSign || up || alreadyDown) return false;
        if (rightAlt)
        {
            // Pass native AltGr input unchanged, including dead keys. Never register these
            // Ctrl+Alt chords globally: ignoring a WM_HOTKEY would already lose the character.
            if (key != 'G' || leftAlt || rightControl ||
                (modifiers != HotkeyModifiers.Alt && modifiers != (HotkeyModifiers.Control | HotkeyModifiers.Alt))) return false;
            consumed[key] = true;
            sendAtSign();
            return true;
        }
        var binding = configuration.HookWindowBindings.FirstOrDefault(b => b.VirtualKey == key && b.Modifiers == modifiers);
        if (binding is null) return false;
        consumed[key] = true;
        windowCommand(binding);
        return true;
    }
}
