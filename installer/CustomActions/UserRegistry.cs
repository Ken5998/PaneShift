using System.Security.Principal;
using Microsoft.Win32;

namespace PaneShift.Installer;

internal static class UserRegistry
{
    // MSI custom actions run in a service host while impersonating the installing
    // user. The predefined HKCU handle can be cached for the service account.
    // Resolve the impersonated identity's loaded hive explicitly instead.
    internal static RegistryKey Open(bool writable = false, string? userSid = null)
    {
        using var identity = WindowsIdentity.GetCurrent();
        string sid = new SecurityIdentifier(userSid ?? identity.User?.Value
            ?? throw new UnauthorizedAccessException("Missing installer user identity.")).Value;
        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        return users.OpenSubKey(sid, writable)
            ?? throw new UnauthorizedAccessException("The installing user's registry hive is unavailable.");
    }
}
