using System.IO;
using System.Net.Http;
using PaneShift.App.Settings;
using PaneShift.Core;
using PaneShift.Windows;

namespace PaneShift.Windows.Tests;

public sealed class UpdateViewModelTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private sealed class Client : IReleaseClient
    {
        public int Calls;
        public AvailableRelease? Release;
        public bool Fail;
        public TaskCompletionSource<AvailableRelease?>? Pending;
        public Task<AvailableRelease?> CheckAsync(Version current, CancellationToken token)
        { Calls++; if (Fail) throw new HttpRequestException(); return Pending?.Task.WaitAsync(token) ?? Task.FromResult(Release); }
        public Task<string> DownloadAsync(ReleaseAsset asset, string directory, CancellationToken token) => throw new NotSupportedException();
    }
    private UpdateViewModel Model(Client client, Action<AvailableRelease>? notify = null) => new(client, new(0, 3, 0),
        new UpdateCheckStore(Path.Combine(directory, "check.json")), notify ?? (_ => { }), _ => { }, (_, _) => Task.CompletedTask, true);

    [Fact]
    public async Task DisabledDoesNoNetworkAndManualCheckStillWorks()
    {
        var client = new Client(); using var model = Model(client);
        await model.CheckAsync(true); Assert.Equal(0, client.Calls);
        await model.CheckAsync(false); Assert.Equal(1, client.Calls);
        Assert.Contains("up to date", model.Status);
    }
    [Fact]
    public async Task FailedAttemptSurvivesRestartAndIsNotRetriedAutomatically()
    {
        var client = new Client { Fail = true };
        using (var model = Model(client)) { model.Configure(true); await model.CheckAsync(true); Assert.Contains("Could not", model.Status); }
        using var restarted = Model(client); restarted.Configure(true); await restarted.CheckAsync(true);
        Assert.Equal(1, client.Calls);
    }
    [Fact]
    public async Task ConcurrentChecksAreCoalescedAndDisablingCancelsAutomaticRequest()
    {
        var client = new Client { Pending = new() }; using var model = Model(client); model.Configure(true);
        Task pending = model.CheckAsync(true); Assert.True(model.IsBusy);
        await model.CheckAsync(false); Assert.Equal(1, client.Calls);
        model.Configure(false); await pending; Assert.False(model.IsBusy); Assert.Contains("cancelled", model.Status);
    }
    [Fact]
    public void UpdateFlagUsesDraftApplyResetAndExternalReload()
    {
        PaneShiftSettings? active = null;
        var model = new SettingsViewModel(new(), candidate => { active = candidate; return null; });
        model.AutomaticUpdates = true; Assert.Null(active); Assert.True(model.HasChanges);
        Assert.True(model.Apply()); Assert.True(active!.Updates.CheckAutomatically);
        model.RestoreDefaults(); Assert.False(model.AutomaticUpdates); Assert.True(active.Updates.CheckAutomatically);
        model.ExternalReload(new()); Assert.True(model.HasExternalChange);
        model.ReloadDraft(); Assert.False(model.AutomaticUpdates);
    }
    [Fact]
    public async Task NewReleaseNotifiesOnceAndNeverInstallsDuringCheck()
    {
        var client = new Client { Release = new(new(0, 4, 0), "v0.4.0", new("https://github.com/Ken5998/PaneShift/releases/tag/v0.4.0"), "Notes", null,
            new("PaneShift-0.4.0-win-x64.zip", new("https://github.com/Ken5998/PaneShift/releases/download/v0.4.0/PaneShift-0.4.0-win-x64.zip"), 10, new string('a', 64))) };
        int notifications = 0; using var model = Model(client, _ => notifications++); model.Configure(true);
        await model.CheckAsync(true); Assert.Equal(1, notifications); Assert.True(model.HasRelease);
        await model.CheckAsync(false); Assert.Equal(1, notifications);
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
