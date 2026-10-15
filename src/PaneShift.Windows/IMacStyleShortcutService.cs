namespace PaneShift.Windows;

/// <summary>Preparation may fail; commit only flips a prepared execution layer.</summary>
public interface IMacStyleShortcutService
{
    string? Prepare(KeyboardShortcutConfiguration configuration);
    void Commit(KeyboardShortcutConfiguration configuration);
    void Rollback();
}
