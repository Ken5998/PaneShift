using PaneShift.Installer;
using PaneShift.Windows;

namespace PaneShift.Windows.Tests;

public sealed class InstallerPolicyTests
{
    [Fact]
    public void InstallerRegistryHandleReadsAndWritesInstallingUsersHive()
    {
        string path = @"Software\PaneShift\RegistryTest-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var user = UserRegistry.Open(true))
            using (var key = user.CreateSubKey(path)) key.SetValue("Owner", "current-user");
            using var expected = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path);
            Assert.Equal("current-user", expected?.GetValue("Owner"));
            using var readUser = UserRegistry.Open();
            using var read = readUser.OpenSubKey(path);
            Assert.Equal("current-user", read?.GetValue("Owner"));
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(path, false); }
    }
    private const string Exe = @"C:\Users\Test\AppData\Local\Programs\PaneShift-MSI\PaneShift.App.exe";
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void UninstallOnlyRemovesOwnStartupOutsideMajorUpgrade(bool uninstall, bool upgrading, bool expected) =>
        Assert.Equal(expected, InstallerPolicy.ShouldRemoveStartup('"' + Exe + '"', Exe, uninstall, upgrading));
    [Fact]
    public void ForeignOrExtendedStartupCommandsArePreserved()
    {
        Assert.False(InstallerPolicy.ShouldRemoveStartup("\"C:\\Portable\\PaneShift.App.exe\"", Exe, true, false));
        Assert.False(InstallerPolicy.OwnsStartup('"' + Exe + "\" --extra", Exe));
        Assert.False(InstallerPolicy.OwnsStartup(null, Exe));
    }
    [Fact]
    public void LegacyStartupIsRedirectedOnlyIfItBelongedToMigratedCopyAndWasNotChanged()
    {
        string command = '"' + Exe + '"';
        Assert.True(InstallerPolicy.ShouldRedirectLegacyStartup(command, null, Exe));
        Assert.True(InstallerPolicy.ShouldRedirectLegacyStartup(command, command, Exe));
        Assert.False(InstallerPolicy.ShouldRedirectLegacyStartup(null, null, Exe));
        Assert.False(InstallerPolicy.ShouldRedirectLegacyStartup(command, "another copy", Exe));
    }
    [Fact]
    public void UpdateJobsCannotEscapePrivateStagingDirectory()
    {
        Assert.Throws<ArgumentException>(() => UpdateInstaller.ValidateJobDirectory(@"C:\Temp\install-job.json"));
        Assert.Throws<ArgumentException>(() => UpdateInstaller.ValidateJobDirectory(Path.Combine(UpdateInstaller.UpdateRoot, "invalid", "install-job.json")));
        Assert.Throws<ArgumentException>(() => UpdateInstaller.ValidateJobDirectory(Path.Combine(UpdateInstaller.UpdateRoot, Guid.NewGuid().ToString("N"), "other.json")));
    }
    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, "", null)]
    [InlineData(null, "legacy", "legacy")]
    [InlineData("foreign", "legacy", "foreign")]
    [InlineData("", "legacy", "")]
    [InlineData("legacy", "foreign", "legacy")]
    public void MissingServiceStartupUsesClientSnapshotWithoutReplacingVisiblePreference(
        string? service, string? client, string? expected) =>
        Assert.Equal(expected, InstallerPolicy.LegacyStartupSnapshot(service, client));

    [Fact]
    public void ClientSnapshotPreservesOnlyOriginalLegacyOptIn()
    {
        string oldExe = @"C:\Users\Test\AppData\Local\Programs\PaneShift\PaneShift.App.exe";
        string oldCommand = '"' + oldExe + '"';
        string newCommand = '"' + Exe + '"';
        string? captured = InstallerPolicy.LegacyStartupSnapshot(null, oldCommand);
        Assert.True(InstallerPolicy.ShouldRedirectLegacyStartup(captured, null, oldExe));
        Assert.False(InstallerPolicy.ShouldRedirectLegacyStartup(captured, newCommand, oldExe));
        Assert.False(InstallerPolicy.ShouldRedirectLegacyStartup(captured, "foreign", oldExe));
        Assert.False(InstallerPolicy.ShouldRedirectLegacyStartup(
            InstallerPolicy.LegacyStartupSnapshot(null, null), null, oldExe));
        Assert.False(InstallerPolicy.ShouldRedirectLegacyStartup(
            InstallerPolicy.LegacyStartupSnapshot(null, "foreign"), null, oldExe));
    }
}
