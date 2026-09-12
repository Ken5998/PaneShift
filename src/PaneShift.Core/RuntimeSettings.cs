namespace PaneShift.Core;

/// <summary>Owns the immutable active snapshot and its repetition state on the command/UI thread.</summary>
public sealed class RuntimeSettings
{
    public PaneShiftSettings Current { get; private set; }
    public CommandRepetition Repetition { get; } = new();

    public RuntimeSettings(PaneShiftSettings? initial = null)
    {
        Current = initial ?? new();
        Current.Validate();
    }

    /// <summary>Returns an error without changing live state if reading or validation fails.</summary>
    public string? Reload(SettingsStore store)
    {
        var candidate = store.LoadCandidate();
        if (candidate.Settings is null) return candidate.Error;
        ReplaceCurrent(candidate.Settings);
        return null;
    }

    // Separate from candidate loading so future hotkey preparation can precede this commit.
    public void ReplaceCurrent(PaneShiftSettings candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        candidate.Validate();
        Current = candidate;
        Repetition.Reset();
    }
}
