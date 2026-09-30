namespace PaneShift.Windows;

/// <summary>Preparation may fail; commit only flips a prepared execution layer.</summary>
public interface IMacStyleShortcutService
{
    string? Prepare(bool enabled);
    void Commit(bool enabled);
    void Rollback();
}
