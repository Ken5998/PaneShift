using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32;
using PaneShift.Core;

namespace PaneShift.Windows;

public static class PaneShiftInstallation
{
    public const string MarkerKey = @"Software\PaneShift\Installer";
    public const string LegacyKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{84308E17-6F94-43D7-9367-38DD674BB41E}_is1";
    public static string MsiDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PaneShift-MSI");
    public static bool IsInstalled(string directory)
    {
        string full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        using var marker = Registry.CurrentUser.OpenSubKey(MarkerKey);
        if (string.Equals(full, (marker?.GetValue("Path") as string)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return true;
        using var legacy = Registry.CurrentUser.OpenSubKey(LegacyKey);
        return legacy?.GetValue("InstallLocation") is string location && string.Equals(full, location.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record UpdateInstallJob(int ParentId, long ParentStartedUtcTicks, string Installer, string Sha256,
    string Version, string OriginalExecutable);

/// <summary>A relocated copy waits for normal application exit, then opens Windows Installer.</summary>
public static class UpdateInstaller
{
    public static string UpdateRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaneShift", "Updates");

    public static string CreateDownloadDirectory()
    {
        string directory = Path.Combine(UpdateRoot, Guid.NewGuid().ToString("N"));
        ValidateJobDirectory(Path.Combine(directory, "install-job.json"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static void StartHelper(string downloaded, ReleaseAsset asset, Version version, string sourceDirectory)
    {
        if (!PaneShiftInstallation.IsInstalled(sourceDirectory)) throw new InvalidOperationException("Use the portable ZIP for this copy.");
        string directory = Path.GetDirectoryName(downloaded)!;
        ValidateJobDirectory(Path.Combine(directory, "install-job.json"));
        if (Path.GetFileName(downloaded) != $"PaneShift-{version}-Setup-x64.msi") throw new ArgumentException("Unexpected installer.");
        string helper = Path.Combine(directory, "helper");
        // The installed files may be replaced during MSI upgrade. Relocate the current runtime first.
        CopyRuntime(sourceDirectory, helper);
        using var parent = Process.GetCurrentProcess();
        var job = new UpdateInstallJob(parent.Id, parent.StartTime.ToUniversalTime().Ticks, downloaded, asset.Sha256,
            version.ToString(), Path.Combine(sourceDirectory, "PaneShift.App.exe"));
        string jobPath = Path.Combine(directory, "install-job.json");
        File.WriteAllText(jobPath, JsonSerializer.Serialize(job));
        var start = new ProcessStartInfo(Path.Combine(helper, "PaneShift.App.exe")) { UseShellExecute = false, WorkingDirectory = helper };
        start.ArgumentList.Add("--install-update"); start.ArgumentList.Add(jobPath);
        using var process = Process.Start(start) ?? throw new IOException("Could not start the update helper.");
    }

    public static void ValidateJobDirectory(string path)
    {
        string full = Path.GetFullPath(path), parent = Path.GetDirectoryName(full)!;
        if (Path.GetFileName(full) != "install-job.json" || !string.Equals(Path.GetDirectoryName(parent), UpdateRoot, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(Path.GetFileName(parent), "N", out _)) throw new ArgumentException("Invalid update job location.");
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        for (string? probe = parent; probe is not null && probe.StartsWith(local, StringComparison.OrdinalIgnoreCase); probe = Path.GetDirectoryName(probe))
            if (Directory.Exists(probe) && File.GetAttributes(probe).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Redirected update directory.");
    }

    private static void CopyRuntime(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Redirected runtime directory.");
            CopyRuntime(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
        foreach (string file in Directory.EnumerateFiles(source))
        {
            if (File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Redirected runtime file.");
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
        }
    }

    public static async Task<string?> RunHelperAsync(string jobPath)
    {
        if (ProcessPrivileges.IsCurrentProcessElevated) throw new InvalidOperationException("Updates require a standard user instance.");
        ValidateJobDirectory(jobPath);
        var job = JsonSerializer.Deserialize<UpdateInstallJob>(File.ReadAllText(jobPath)) ?? throw new ArgumentException("Missing update job.");
        var version = ReleaseUpdates.StableVersion(job.Version);
        string directory = Path.GetDirectoryName(Path.GetFullPath(jobPath))!;
        if (Path.GetFullPath(job.Installer) != Path.Combine(directory, $"PaneShift-{version}-Setup-x64.msi") ||
            job.Sha256.Length != 64 || !job.Sha256.All(Uri.IsHexDigit) || !Path.IsPathFullyQualified(job.OriginalExecutable) ||
            Path.GetFileName(job.OriginalExecutable) != "PaneShift.App.exe" || !PaneShiftInstallation.IsInstalled(Path.GetDirectoryName(job.OriginalExecutable)!))
            throw new ArgumentException("Invalid update installation job.");
        try { return await ExecuteJobAsync(job, directory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // If the parent is still alive, do not launch a duplicate. Once it exited,
            // failed installation should leave the previous executable usable.
            bool parentAlive = false;
            try { using var parent = Process.GetProcessById(job.ParentId); parentAlive = parent.StartTime.ToUniversalTime().Ticks == job.ParentStartedUtcTicks; }
            catch (ArgumentException) { }
            if (!parentAlive) TryRestart(job.OriginalExecutable);
            return "The update failed. The previous copy was restarted where available. Review install.log or open PaneShift from Start.";
        }
    }

    private static void TryRestart(string executable)
    {
        try { if (File.Exists(executable)) { using var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true }); } }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    private static async Task<string?> ExecuteJobAsync(UpdateInstallJob job, string directory)
    {
        try
        {
            using var parent = Process.GetProcessById(job.ParentId);
            if (parent.StartTime.ToUniversalTime().Ticks != job.ParentStartedUtcTicks) throw new IOException("The update parent identity changed.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await parent.WaitForExitAsync(timeout.Token);
        }
        catch (ArgumentException) { /* The previous instance has already exited. */ }
        catch (OperationCanceledException) { return "PaneShift did not exit. Close it normally and retry the update."; }
        // Recheck immediately before execution; a failed/cancelled download is never executed.
        await using (var file = new FileStream(job.Installer, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (!Convert.ToHexString(await SHA256.HashDataAsync(file)).Equals(job.Sha256, StringComparison.OrdinalIgnoreCase))
            { TryRestart(job.OriginalExecutable); return "Installer verification failed. No update was installed; the previous copy was restarted where available."; }
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "msiexec.exe")) { UseShellExecute = false };
            foreach (string argument in new[] { "/i", job.Installer, "/norestart", "/l*v", Path.Combine(directory, "install.log") }) start.ArgumentList.Add(argument);
            using var installer = Process.Start(start) ?? throw new IOException("Could not launch Windows Installer.");
            await installer.WaitForExitAsync();
            bool success = installer.ExitCode is 0 or 3010;
            string replacement = Path.Combine(PaneShiftInstallation.MsiDirectory, "PaneShift.App.exe");
            string launch = success ? replacement : job.OriginalExecutable;
            if (success && (!File.Exists(launch) || FileVersionInfo.GetVersionInfo(launch).ProductVersion != job.Version))
                return "Installation finished, but its version could not be verified. Open PaneShift from Start and review install.log.";
            if (File.Exists(launch))
            {
                using var process = Process.Start(new ProcessStartInfo(launch) { UseShellExecute = true });
            }
            return success ? null : installer.ExitCode == 1602 ? "Update cancelled. The previous PaneShift copy has been restarted." : $"Windows Installer returned {installer.ExitCode}. The previous copy has been restarted where available. See install.log.";
        }
    }
}
