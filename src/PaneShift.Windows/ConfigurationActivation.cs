using PaneShift.Core;

namespace PaneShift.Windows;

/// <summary>One validated activation path for GUI Apply and explicit file reload.</summary>
public sealed class ConfigurationActivation(RuntimeSettings runtime, SettingsStore store, GlobalHotkeys hotkeys)
{
    public string? Apply(PaneShiftSettings candidate, bool persist, bool paused)
    {
        try
        {
            candidate.Validate();
            var bindings = HotkeySettings.Resolve(candidate);
            return hotkeys.Transition(bindings, () =>
            {
                if (persist) store.Save(candidate);
                runtime.ReplaceCurrent(candidate);
            });
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return $"Settings were not applied. {ex.Message}";
        }
        finally { if (paused) hotkeys.ReleaseAll(); }
    }

    public string? Reload(bool paused)
    {
        var candidate = store.LoadCandidate();
        return candidate.Settings is null ? candidate.Error : Apply(candidate.Settings, false, paused);
    }
}
