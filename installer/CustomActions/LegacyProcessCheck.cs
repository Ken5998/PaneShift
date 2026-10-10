using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PaneShift.Installer;

internal static class LegacyProcessCheck
{
    // Older releases only have a session-local mutex. Query image paths once at
    // installer preflight; never stop a process or inspect its memory.
    internal static bool IsRunning(string executable)
    {
        foreach (var process in Process.GetProcessesByName("PaneShift.App"))
        {
            using (process)
            using (var handle = OpenProcess(0x1000, false, process.Id))
            {
                if (handle.IsInvalid) continue;
                var path = new StringBuilder(32768);
                int length = path.Capacity;
                if (QueryFullProcessImageName(handle, 0, path, ref length) &&
                    string.Equals(path.ToString(), executable, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder name, ref int length);
}
