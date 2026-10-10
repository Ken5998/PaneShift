namespace PaneShift.Installer;

/// <summary>Shared decisions for conditional startup cleanup and migration.</summary>
public static class InstallerPolicy
{
    public static bool OwnsStartup(string? command, string executable) =>
        string.Equals(command, "\"" + executable + "\"", StringComparison.OrdinalIgnoreCase);
    public static bool ShouldRemoveStartup(string? command, string executable, bool uninstall, bool upgrading) =>
        uninstall && !upgrading && OwnsStartup(command, executable);
    public static string? LegacyStartupSnapshot(string? serviceCommand, string? clientCommand) =>
        serviceCommand ?? (string.IsNullOrEmpty(clientCommand) ? null : clientCommand);
    public static bool ShouldRedirectLegacyStartup(string? before, string? after, string legacyExecutable) =>
        OwnsStartup(before, legacyExecutable) && (after is null || string.Equals(before, after, StringComparison.OrdinalIgnoreCase));
}
