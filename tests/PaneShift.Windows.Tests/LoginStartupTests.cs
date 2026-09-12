using PaneShift.App.Settings;

namespace PaneShift.Windows.Tests;

public sealed class LoginStartupTests
{
    private const string Executable = @"C:\Users\User Name\Programs\PaneShift\PaneShift.App.exe";
    private sealed class Store : ILoginStartupStore
    {
        public string? Value;
        public int Writes, Deletes;
        public bool DenyRead, DenyWrite;
        public string? Read() => DenyRead ? throw new UnauthorizedAccessException("Test read failure") : Value;
        public void Write(string command)
        {
            if (DenyWrite) throw new UnauthorizedAccessException("Test write failure");
            Value = command; Writes++;
        }
        public void Delete()
        {
            if (DenyWrite) throw new UnauthorizedAccessException("Test delete failure");
            Value = null; Deletes++;
        }
    }

    [Fact]
    public void CommandQuotesPathAndContainsNoRestartOrElevationArguments()
    {
        Assert.Equal('"' + Executable + '"', LoginStartup.CreateCommand(Executable));
    }

    [Theory]
    [InlineData("PaneShift.App.exe")]
    [InlineData(@"C:\PaneShift.dll")]
    [InlineData("C:\\bad\"path.exe")]
    [InlineData("C:\\bad\npath.exe")]
    public void InvalidExecutablePathsAreRejected(string path) => Assert.Throws<ArgumentException>(() => LoginStartup.CreateCommand(path));

    [Fact]
    public void WindowsRunCommandLengthIsRespected() => Assert.Throws<ArgumentException>(() => LoginStartup.CreateCommand("C:\\" + new string('a', 260) + ".exe"));

    [Fact]
    public void EnableAndDisableAreIdempotent()
    {
        var store = new Store();
        var startup = new LoginStartup(store, Executable);
        Assert.False(startup.Read().IsEnabled);
        Assert.Null(startup.SetEnabled(true));
        Assert.Null(startup.SetEnabled(true));
        Assert.True(startup.Read().IsEnabled);
        Assert.Equal(1, store.Writes);
        Assert.Null(startup.SetEnabled(false));
        Assert.Null(startup.SetEnabled(false));
        Assert.Equal(1, store.Deletes);
        Assert.False(startup.Read().IsEnabled);
    }

    [Fact]
    public void DisablingDoesNotRemoveADifferentCopiesRegistration()
    {
        var store = new Store { Value = @"""D:\Portable\PaneShift.App.exe""" };
        var startup = new LoginStartup(store, Executable);
        Assert.False(startup.Read().IsEnabled);
        Assert.Contains("Another PaneShift location", startup.Read().Description);
        Assert.Null(startup.SetEnabled(false));
        Assert.Equal(0, store.Deletes);
        Assert.NotNull(store.Value);
        Assert.Null(startup.SetEnabled(true));
        Assert.Equal(LoginStartup.CreateCommand(Executable), store.Value);
    }

    [Fact]
    public void PathsAreComparedCaseInsensitively()
    {
        var store = new Store { Value = LoginStartup.CreateCommand(Executable).ToUpperInvariant() };
        var startup = new LoginStartup(store, Executable);
        Assert.True(startup.Read().IsEnabled);
        Assert.Null(startup.SetEnabled(true));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void ReadFailureDisablesControlAndReportsError()
    {
        var state = new LoginStartup(new Store { DenyRead = true }, Executable).Read();
        Assert.False(state.CanChange);
        Assert.Contains("Could not read", state.Description);
    }

    [Fact]
    public void WriteFailureKeepsActualStateAndVisibleError()
    {
        var model = new LoginStartupViewModel(new(new Store { DenyWrite = true }, Executable));
        model.ToggleCommand.Execute(null);
        Assert.False(model.IsEnabled);
        Assert.Contains("Could not change", model.Error);
    }

    [Fact]
    public void DeleteFailureKeepsTheCheckedState()
    {
        var store = new Store { Value = LoginStartup.CreateCommand(Executable), DenyWrite = true };
        var model = new LoginStartupViewModel(new(store, Executable));
        model.IsEnabled = false;
        Assert.True(model.IsEnabled);
        Assert.NotEmpty(model.Error);
    }

    [Fact]
    public void UnavailableModeNeverWrites()
    {
        var store = new Store();
        var startup = new LoginStartup(store, Executable, "Use a standard Release instance.");
        Assert.False(startup.Read().CanChange);
        Assert.NotNull(startup.SetEnabled(true));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void SharedStartupSettingDoesNotAlterOrDiscardTheJsonDraft()
    {
        var store = new Store();
        var startup = new LoginStartupViewModel(new(store, Executable));
        var settings = new SettingsViewModel(new(), _ => throw new Exception("JSON Apply must not run"), startup);
        startup.IsEnabled = true;
        Assert.True(settings.LoginStartup!.IsEnabled);
        Assert.False(settings.HasChanges);
        settings.GapText = "12";
        store.Value = null;
        startup.Refresh();
        Assert.False(settings.LoginStartup.IsEnabled);
        Assert.Equal("12", settings.GapText);
        Assert.True(settings.HasChanges);
    }
}
