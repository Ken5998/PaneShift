using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PaneShift.Windows;

public static class ProcessPrivileges
{
    public static bool IsCurrentProcessElevated
    {
        get
        {
            const uint tokenQuery = 0x0008;
            const int tokenElevation = 20;
            if (!OpenProcessToken(new nint(-1), tokenQuery, out var token))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            using (token)
            {
                if (!GetTokenInformation(token, tokenElevation, out uint elevated, sizeof(uint), out _))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                return elevated != 0;
            }
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(nint process, uint desiredAccess, out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass,
        out uint information, int informationLength, out int returnLength);
}
