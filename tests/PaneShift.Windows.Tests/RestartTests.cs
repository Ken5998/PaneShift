namespace PaneShift.Windows.Tests;

public class RestartTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HandoffRoundTripsOriginalSettingsPathAndPauseState(bool paused)
    {
        var request = new RestartRequest(123, new DateTime(2026, 1, 1).Ticks,
            @"C:\Users\A User\AppData\Local\PaneShift\settings.json", paused);
        Assert.Equal(request, RestartRequest.Parse(request.ToArguments()));
        var start = ElevatedRestart.CreateStartInfo(@"C:\Program Files\PaneShift\PaneShift.App.exe", request);
        Assert.True(start.UseShellExecute);
        Assert.Equal("runas", start.Verb);
        Assert.Equal(request.ToArguments(), start.ArgumentList);
        Assert.Equal(@"C:\Program Files\PaneShift", start.WorkingDirectory);
    }

    [Fact]
    public void NormalStartupDoesNotRequestHandoff() => Assert.Null(RestartRequest.Parse([]));

    [Theory]
    [InlineData("--other", "123", "100", @"C:\settings.json", "active")]
    [InlineData("--restart-from", "0", "100", @"C:\settings.json", "active")]
    [InlineData("--restart-from", "123", "0", @"C:\settings.json", "active")]
    [InlineData("--restart-from", "123", "100", "settings.json", "active")]
    [InlineData("--restart-from", "123", "100", @"C:\settings.json", "unknown")]
    public void InvalidHandoffArgumentsAreRejected(params string[] arguments) =>
        Assert.Throws<ArgumentException>(() => RestartRequest.Parse(arguments));

    [Theory]
    [InlineData(1223, RestartOutcome.Cancelled)]
    [InlineData(5, RestartOutcome.Failed)]
    [InlineData(2, RestartOutcome.Failed)]
    public void UacCancellationIsDistinctFromOtherLaunchFailures(int code, RestartOutcome outcome)
    {
        var result = ElevatedRestart.FromLaunchError(code, "native detail");
        Assert.Equal(outcome, result.Outcome);
        Assert.Contains(code.ToString(), result.Details);
    }

    [Fact]
    public void SelfWaitIsRejectedWithoutWaiting() => Assert.Throws<ArgumentException>(() =>
        ElevatedRestart.WaitForPreviousInstance(new(Environment.ProcessId, 1, @"C:\settings.json", false)));
}
