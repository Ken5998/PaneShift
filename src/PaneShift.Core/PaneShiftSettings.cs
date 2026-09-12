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

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegative(GapPixels);
        if (RepeatedCommands is null || RepeatedCommands.HalfActions != RepeatBehavior.CycleSizes)
            throw new ArgumentException("repeatedCommands.halfActions must be 'cycleSizes'.");
    }
}
