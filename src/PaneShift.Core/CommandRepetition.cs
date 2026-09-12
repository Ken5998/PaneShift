namespace PaneShift.Core;

/// <summary>Tracks consecutive commands at invocation time, independently of layout geometry.</summary>
public sealed class CommandRepetition
{
    public nint Target { get; private set; }
    public WindowAction? Action { get; private set; }
    public int Index { get; private set; }
    private int previousLength;

    public int Next(nint target, WindowAction action, int sequenceLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequenceLength);
        if (target == 0 || action == WindowAction.Restore || sequenceLength == 1)
        {
            Reset();
            return 0;
        }
        Index = Target == target && Action == action && previousLength == sequenceLength
            ? (Index + 1) % sequenceLength : 0;
        Target = target;
        Action = action;
        previousLength = sequenceLength;
        return Index;
    }

    public void Reset()
    {
        Target = 0;
        Action = null;
        Index = 0;
        previousLength = 0;
    }
}
