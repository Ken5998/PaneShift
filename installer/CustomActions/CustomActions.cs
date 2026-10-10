using System.Diagnostics;
using Microsoft.Win32;
using WixToolset.Dtf.WindowsInstaller;
using FileAttributes = System.IO.FileAttributes;

namespace PaneShift.Installer;

public static class CustomActions
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string LegacyKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{84308E17-6F94-43D7-9367-38DD674BB41E}_is1";
    private static string? ReadStartup(string userSid) { using var user = UserRegistry.Open(userSid: userSid); using var key = user.OpenSubKey(RunKey); return key?.GetValue("PaneShift") as string; }
    private static void WriteStartup(string command, string userSid) { using var user = UserRegistry.Open(true, userSid); using var key = user.CreateSubKey(RunKey); key.SetValue("PaneShift", command, RegistryValueKind.String); }

    [CustomAction]
    public static ActionResult Prepare(Session session)
    {
        try
        {
            string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PaneShift-MSI");
            string directory = Path.GetFullPath(session["INSTALLFOLDER"]).TrimEnd('\\');
            if (!string.Equals(directory, expected, StringComparison.OrdinalIgnoreCase)) return Error(session, "Use the default per-user PaneShift directory. Other installer locations are not supported.");
            if (HasRedirectedAncestor(directory)) return Error(session, "The installation directory is redirected. Use a normal per-user directory.");
            try
            {
                using var mutex = Mutex.OpenExisting(PaneShift.Windows.InstallerInstanceLock.ForUser(session["UserSID"]));
                bool acquired;
                try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) return Error(session, "Close PaneShift from its notification icon before installing, repairing or uninstalling it. No application will be forcibly closed.");
                mutex.ReleaseMutex();
            }
            catch (WaitHandleCannotBeOpenedException) { }
            catch (UnauthorizedAccessException) { return Error(session, "Close the administrator PaneShift instance before continuing."); }

            string executable = Path.Combine(directory, "PaneShift.App.exe");
            string userSid = session["UserSID"];
            string? startup = ReadStartup(userSid);
            var data = new CustomActionData { { "Executable", executable }, { "Startup", startup ?? "" }, { "UserSid", userSid } };
            session["RemoveStartup"] = data.ToString(); session["RestoreStartup"] = data.ToString();
            string legacyDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PaneShift");
            if (LegacyProcessCheck.IsRunning(Path.Combine(legacyDirectory, "PaneShift.App.exe")))
                return Error(session, "Close the previous PaneShift copy from its notification icon before migrating. No application will be forcibly closed.");
            using var user = UserRegistry.Open(userSid: userSid);
            using var legacy = user.OpenSubKey(LegacyKey);
            if (session["REMOVE"] != "ALL" && string.IsNullOrEmpty(session["Installed"]))
            {
                string uninstaller = Path.Combine(legacyDirectory, "unins000.exe");
                // Native AppSearch also runs in the client UI sequence. Keep those
                // validated values when the service has a different registry view.
                string? location = legacy?.GetValue("InstallLocation") as string;
                string? uninstallCommand = legacy?.GetValue("UninstallString") as string;
                if (location is null && session["LEGACY_LOCATION"].Length != 0)
                { location = session["LEGACY_LOCATION"]; uninstallCommand = session["LEGACY_UNINSTALL"]; }
                if (location is null)
                {
                    if (legacy is not null || File.Exists(uninstaller))
                        return Error(session, "The previous installation record could not be read. Open this MSI interactively, or uninstall the old PaneShift from Windows Settings first. Your preferences are retained.");
                    return ActionResult.Success;
                }
                if (location is null || !string.Equals(location.TrimEnd('\\'), legacyDirectory, StringComparison.OrdinalIgnoreCase))
                    return Error(session, "The old PaneShift installation uses a custom location. Uninstall it from Windows Settings before installing this MSI; your preferences will be retained.");
                if (!string.Equals(uninstallCommand, "\"" + uninstaller + "\"", StringComparison.OrdinalIgnoreCase))
                    return Error(session, "The previous uninstaller command is not the expected default. Uninstall it manually before migrating; settings will be retained.");
                if (!File.Exists(uninstaller)) return Error(session, "The previous PaneShift uninstaller is missing. Repair or uninstall that installation before continuing.");
                if (HasRedirectedAncestor(legacyDirectory) || File.GetAttributes(uninstaller).HasFlag(FileAttributes.ReparsePoint))
                    return Error(session, "The old installation is redirected. Uninstall it manually before migrating.");
                data.Add("LegacyUninstaller", uninstaller);
                data.Add("LegacyHash", HashFile(uninstaller));
                data.Add("LegacyExecutable", Path.Combine(legacyDirectory, "PaneShift.App.exe"));
                // Preserve the client's native snapshot if the service could not
                // see the Run value. Ownership is checked before any redirection.
                data["Startup"] = InstallerPolicy.LegacyStartupSnapshot(startup, session["STARTUP_COMMAND"]) ?? "";
                session["MigrateLegacy"] = data.ToString();
            }
            return ActionResult.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException or System.ComponentModel.Win32Exception)
        { return Error(session, "PaneShift could not validate the installation. No old installation was removed."); }
    }

    [CustomAction]
    public static ActionResult RemoveStartup(Session session)
    {
        string executable = session.CustomActionData["Executable"];
        string userSid = session.CustomActionData["UserSid"];
        if (InstallerPolicy.OwnsStartup(ReadStartup(userSid), executable))
        { using var user = UserRegistry.Open(true, userSid); using var key = user.OpenSubKey(RunKey, true); key?.DeleteValue("PaneShift", false); }
        return ActionResult.Success;
    }

    [CustomAction]
    public static ActionResult RestoreStartup(Session session)
    {
        string original = session.CustomActionData["Startup"], executable = session.CustomActionData["Executable"];
        string userSid = session.CustomActionData["UserSid"];
        if (InstallerPolicy.OwnsStartup(original, executable) && ReadStartup(userSid) is null) WriteStartup(original, userSid);
        return ActionResult.Success;
    }

    [CustomAction]
    public static ActionResult MigrateLegacy(Session session)
    {
        // Commit phase: never remove Inno before the new MSI transaction has succeeded.
        var data = session.CustomActionData;
        if (!data.ContainsKey("LegacyUninstaller")) return ActionResult.Success;
        try
        {
            if (HashFile(data["LegacyUninstaller"]) != data["LegacyHash"]) throw new IOException("The previous uninstaller changed during installation.");
            // The new payload is committed. Redirect first so Inno's conditional cleanup
            // cannot remove the preserved preference, even if its uninstall takes longer.
            RedirectLegacyStartup(data);
            var start = new ProcessStartInfo(data["LegacyUninstaller"], "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            using var process = Process.Start(start);
            if (process is null || !process.WaitForExit(120000) || process.ExitCode != 0)
                throw new IOException("Previous installer did not finish successfully.");
            // Recheck after cleanup too: restore only an originally owned Run value
            // that is absent or still points to Inno, never a different copy's entry.
            RedirectLegacyStartup(data);
            // Inno's uninstall log owns the old Start Menu name; restore the MSI-owned
            // shortcut after it removes that shared name.
            string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "PaneShift.lnk");
            if (!File.Exists(shortcut))
            {
                var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                try
                {
                    dynamic link = ((dynamic)shell!).CreateShortcut(shortcut);
                    try { link.TargetPath = data["Executable"]; link.WorkingDirectory = Path.GetDirectoryName(data["Executable"]); link.IconLocation = data["Executable"] + ",0"; link.Save(); }
                    finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link); }
                }
                finally { if (shell is not null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Security.SecurityException or System.Runtime.InteropServices.COMException)
        {
            // The MSI is already committed. Preserve its success and report incomplete migration explicitly.
            session.Log("PaneShift MSI installed, but the old installer could not be fully removed. Review Windows Installed apps and startup preferences.");
            using var record = new Record(0) { FormatString = "PaneShift was installed, but migration from the old installer was incomplete. Review Installed apps and your start-at-sign-in setting. Your settings file has been retained." };
            session.Message(InstallMessage.Warning, record);
        }
        return ActionResult.Success;
    }

    private static void RedirectLegacyStartup(CustomActionData data)
    {
        string? before = data["Startup"].Length == 0 ? null : data["Startup"];
        string userSid = data["UserSid"];
        if (!InstallerPolicy.ShouldRedirectLegacyStartup(before, ReadStartup(userSid), data["LegacyExecutable"])) return;
        string replacement = "\"" + data["Executable"] + "\"";
        WriteStartup(replacement, userSid);
        if (!string.Equals(ReadStartup(userSid), replacement, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Windows did not retain the migrated startup command.");
    }

    private static ActionResult Error(Session session, string message)
    {
        using var record = new Record(0) { FormatString = message };
        session.Message(InstallMessage.Error, record);
        return ActionResult.Failure;
    }

    private static string HashFile(string path)
    {
        using var hash = System.Security.Cryptography.SHA256.Create(); using var stream = File.OpenRead(path);
        return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
    }
    private static bool HasRedirectedAncestor(string directory)
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        for (string? probe = directory; probe is not null && probe.StartsWith(local, StringComparison.OrdinalIgnoreCase); probe = Path.GetDirectoryName(probe))
            if (Directory.Exists(probe) && File.GetAttributes(probe).HasFlag(FileAttributes.ReparsePoint)) return true;
        return false;
    }
}
