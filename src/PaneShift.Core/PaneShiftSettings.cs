using System.Collections.Immutable;

namespace PaneShift.Core;

public enum RepeatBehavior { CycleSizes }

public sealed record RepeatedCommandSettings
{
    public RepeatBehavior HalfActions { get; init; } = RepeatBehavior.CycleSizes;
}

public sealed record PaneShiftSettings
{
    public int GapPixels { get; init; }
    public bool ApplyGapToScreenEdges { get; init; }
    public RepeatedCommandSettings RepeatedCommands { get; init; } = new();
    public MacStyleShortcutSettings MacStyleShortcuts { get; init; } = new();
    public UpdateSettings Updates { get; init; } = new();
    public ImmutableSortedDictionary<string, string?>? Hotkeys { get; init; }

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(GapPixels);
        if (MacStyleShortcuts is null) throw new ArgumentException("macStyleShortcuts must be an object.");
        if (Updates is null) throw new ArgumentException("updates must be an object.");
        if (RepeatedCommands is null || RepeatedCommands.HalfActions != RepeatBehavior.CycleSizes)
            throw new ArgumentException("repeatedCommands.halfActions must be 'cycleSizes'.");
        _ = HotkeySettings.Resolve(this);
    }
}
