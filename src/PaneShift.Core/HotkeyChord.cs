using System.Globalization;

namespace PaneShift.Core;

public readonly record struct HotkeyChord(HotkeyModifiers Modifiers, uint VirtualKey)
{
    private static readonly Dictionary<uint, string> KeyNames = new()
    {
        [0x08] = "Backspace", [0x09] = "Tab", [0x0D] = "Enter", [0x1B] = "Escape", [0x20] = "Space",
        [0x21] = "PageUp", [0x22] = "PageDown", [0x23] = "End", [0x24] = "Home",
        [0x25] = "Left", [0x26] = "Up", [0x27] = "Right", [0x28] = "Down", [0x2D] = "Insert", [0x2E] = "Delete",
        [0x6A] = "Multiply", [0x6B] = "Add", [0x6D] = "Subtract", [0x6E] = "Decimal", [0x6F] = "Divide"
    };

    public void Validate()
    {
        if (Modifiers == HotkeyModifiers.None || ((uint)Modifiers & ~15u) != 0)
            throw new ArgumentException("Use at least one of Ctrl, Alt, Shift or Win with a key.");
        if (VirtualKey < 8 || VirtualKey > 254 || VirtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or >= 0xA0 and <= 0xA5)
            throw new ArgumentException("Choose a non-modifier key for the shortcut.");
        if (VirtualKey == 0x7B) throw new ArgumentException("F12 is reserved by Windows for debugging; choose another key.");
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        string key = VirtualKey switch
        {
            >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)VirtualKey).ToString(),
            >= 0x70 and <= 0x87 => $"F{VirtualKey - 0x6F}",
            >= 0x60 and <= 0x69 => $"Num{VirtualKey - 0x60}",
            _ => KeyNames.GetValueOrDefault(VirtualKey, $"VK{VirtualKey:X2}")
        };
        parts.Add(key);
        return string.Join("+", parts);
    }

    public string Display => ToString().Replace("+", " + ");

    public static HotkeyChord Parse(string text)
    {
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) throw new ArgumentException("A shortcut needs a modifier and a key.");
        HotkeyModifiers modifiers = 0;
        foreach (string part in parts[..^1])
        {
            var modifier = part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => HotkeyModifiers.Control, "ALT" => HotkeyModifiers.Alt,
                "SHIFT" => HotkeyModifiers.Shift, "WIN" or "WINDOWS" => HotkeyModifiers.Windows,
                _ => throw new ArgumentException($"Unknown shortcut modifier: {part}.")
            };
            if (modifiers.HasFlag(modifier)) throw new ArgumentException($"Repeated modifier: {part}.");
            modifiers |= modifier;
        }
        string name = parts[^1].ToUpperInvariant();
        uint key;
        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0])) key = name[0];
        else if (name.StartsWith('F') && uint.TryParse(name[1..], out uint f) && f is >= 1 and <= 24) key = 0x6F + f;
        else if (name.StartsWith("NUM") && uint.TryParse(name[3..], out uint num) && num <= 9) key = 0x60 + num;
        else if (name.StartsWith("VK") && uint.TryParse(name[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint vk)) key = vk;
        else key = KeyNames.FirstOrDefault(p => p.Value.Equals(name, StringComparison.OrdinalIgnoreCase)).Key;
        var chord = new HotkeyChord(modifiers, key);
        chord.Validate();
        return chord;
    }
}
