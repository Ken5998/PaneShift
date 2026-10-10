using System.Security.Principal;

namespace PaneShift.Windows;

/// <summary>Lets the MSI service observe a user's running copy across Windows sessions.</summary>
public static class InstallerInstanceLock
{
    public static string ForUser(string userSid) => @"Global\PaneShift-" + new SecurityIdentifier(userSid).Value;
    public static string CurrentUserName
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return ForUser(identity.User?.Value ?? throw new UnauthorizedAccessException("Missing user identity."));
        }
    }
}
