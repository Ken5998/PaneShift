using Microsoft.Win32;

namespace PaneShift.Windows;

public interface ILoginStartupStore
{
    string? Read();
    void Write(string command);
    void Delete();
}

public sealed class RegistryLoginStartupStore : ILoginStartupStore
{
    public const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "PaneShift";

    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        var value = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch { null => null, string command => command, _ => throw new IOException("The PaneShift startup entry is not a command string.") };
    }
    public void Write(string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }
    public void Delete()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}

public sealed record LoginStartupState(bool IsEnabled, bool CanChange, string Description);

/// <summary>Explicit per-user opt-in, independent of the JSON layout/shortcut draft.</summary>
public sealed class LoginStartup(ILoginStartupStore store, string executable, string? unavailableReason = null)
{
    public static string CreateCommand(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            executable.IndexOfAny(['"', '\r', '\n']) >= 0)
            throw new ArgumentException("A stable, absolute executable path is required.");
        string command = $"\"{executable}\"";
        if (command.Length > 260) throw new ArgumentException("This path is too long for Windows startup. Move PaneShift to a shorter path.");
        return command;
    }

    public LoginStartupState Read()
    {
        try
        {
            string command = CreateCommand(executable);
            string? current = store.Read();
            bool enabled = string.Equals(command, current, StringComparison.OrdinalIgnoreCase);
            string description = unavailableReason ?? (current is not null && !enabled
                ? "Another PaneShift location is registered. Turn this on to use the current copy instead."
                : "Starts this copy in the tray with standard privileges. Keep its folder in place. Windows Startup apps can override this registration.");
            return new(enabled, unavailableReason is null, description);
        }
        catch (Exception ex) when (IsExpected(ex)) { return new(false, false, $"Could not read startup settings. {ex.Message}"); }
    }

    public string? SetEnabled(bool enabled)
    {
        if (unavailableReason is not null) return unavailableReason;
        try
        {
            string command = CreateCommand(executable);
            string? current = store.Read();
            if (enabled)
            {
                if (!string.Equals(current, command, StringComparison.OrdinalIgnoreCase)) store.Write(command);
            }
            else if (string.Equals(current, command, StringComparison.OrdinalIgnoreCase)) store.Delete();
            // Do not remove a different portable/installed copy's entry.
            var state = Read();
            if (!state.CanChange || state.IsEnabled != enabled) return "Windows did not retain the startup change. " + state.Description;
            return null;
        }
        catch (Exception ex) when (IsExpected(ex)) { return $"Could not change startup settings. {ex.Message}"; }
    }

    private static bool IsExpected(Exception ex) => ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException;
}
