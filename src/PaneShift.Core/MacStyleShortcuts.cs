namespace PaneShift.Core;

public sealed record MacStyleShortcutSettings
{
    public bool Enabled { get; init; }
}

public sealed record ShortcutTranslation(string Name, HotkeyChord Source, HotkeyChord Target)
{
    public string SourceDisplay => Source.Display;
    public string TargetDisplay => Target.Display;
}

/// <summary>The complete, exact whitelist. Extra modifiers never match.</summary>
public static class MacStyleShortcuts
{
    public static IReadOnlyList<ShortcutTranslation> All { get; } = Array.AsReadOnly(new[]
    {
        Map("Copy", "Win+C", "Ctrl+C"), Map("Paste", "Win+V", "Ctrl+V"),
        Map("Cut", "Win+X", "Ctrl+X"), Map("Select all", "Win+A", "Ctrl+A"),
        Map("Undo", "Win+Z", "Ctrl+Z"), Map("Redo", "Win+Shift+Z", "Ctrl+Shift+Z"),
        Map("Save", "Win+S", "Ctrl+S"), Map("Find", "Win+F", "Ctrl+F"),
        Map("New tab", "Win+T", "Ctrl+T"), Map("Reopen closed tab", "Win+Shift+T", "Ctrl+Shift+T"),
        Map("Close tab / document", "Win+W", "Ctrl+W"), Map("Close app / window", "Win+Q", "Alt+F4")
    });

    private static ShortcutTranslation Map(string name, string source, string target) =>
        new(name, HotkeyChord.Parse(source), HotkeyChord.Parse(target));

    public static ShortcutTranslation? Find(HotkeyChord source) => All.FirstOrDefault(m => m.Source == source);
}
