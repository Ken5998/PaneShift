using System.IO;
using System.Net.Http;
using System.Text.Json;
using PaneShift.Core;
using PaneShift.Windows;

namespace PaneShift.App.Settings;

/// <summary>Shared asynchronous update state; the application owns scheduling and installer handoff.</summary>
public sealed class UpdateViewModel : ObservableObject, IDisposable
{
    private readonly IReleaseClient client;
    private readonly Version current;
    private readonly UpdateCheckStore store;
    private readonly Func<DateTimeOffset> now;
    private readonly Action<AvailableRelease> notify;
    private readonly Action<Uri> openPage;
    private readonly Func<AvailableRelease, CancellationToken, Task> install;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? automaticRequest;
    private bool busy;
    private bool automaticEnabled;
    private string status = "Automatic checks are off. You can check manually.";
    private AvailableRelease? release;
    public UpdateCheckState State { get; private set; }
    public bool IsBusy => busy;
    public string Status => status;
    public bool HasRelease => release is not null;
    public string ReleaseNotes => release?.Notes ?? "";
    public string DownloadLabel { get; }
    public RelayCommand CheckCommand { get; }
    public RelayCommand ReleasePageCommand { get; }
    public RelayCommand InstallCommand { get; }

    public UpdateViewModel(IReleaseClient client, Version current, UpdateCheckStore store,
        Action<AvailableRelease> notify, Action<Uri> openPage, Func<AvailableRelease, CancellationToken, Task> install,
        bool installed, Func<DateTimeOffset>? now = null)
    {
        this.client = client; this.current = current; this.store = store; this.notify = notify;
        this.openPage = openPage; this.install = install; this.now = now ?? (() => DateTimeOffset.UtcNow);
        State = store.Load();
        DownloadLabel = installed ? "Download and install…" : "Download portable ZIP…";
        CheckCommand = new(() => _ = CheckAsync(false), () => !busy);
        ReleasePageCommand = new(() => { if (release is { } value) openPage(value.PageUri); }, () => release is not null && !busy);
        InstallCommand = new(() => _ = InstallAsync(), () => release is not null && !busy);
    }

    public void CancelAutomaticCheck() => automaticRequest?.Cancel();
    public void Configure(bool enabled)
    {
        automaticEnabled = enabled;
        if (!enabled) CancelAutomaticCheck();
        if (!busy && release is null && status.StartsWith("Automatic checks", StringComparison.Ordinal))
        {
            status = enabled ? "Automatic checks are enabled, at most once every 24 hours. You can also check manually." : "Automatic checks are off. You can check manually.";
            Refresh();
        }
    }

    public async Task CheckAsync(bool automatic)
    {
        if (busy || (automatic && (!automaticEnabled || !ReleaseUpdates.IsDue(now(), State.LastAttempt)))) return;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        if (automatic) automaticRequest = request;
        SetBusy(true); status = "Checking GitHub for a stable release…"; Refresh();
        try
        {
            // Persist attempts, including failures, so repeated starts do not hammer the API.
            State = State with { LastAttempt = now() }; store.Save(State);
            release = await client.CheckAsync(current, request.Token);
            status = release is null ? $"PaneShift {current} is up to date. Last checked {now().ToLocalTime():g}." : $"PaneShift {release.Version} is available.";
            if (automatic && release is { } available && State.NotifiedTag != available.Tag)
            {
                State = State with { NotifiedTag = available.Tag }; store.Save(State); notify(available);
            }
        }
        catch (OperationCanceledException) { status = lifetime.IsCancellationRequested || request.IsCancellationRequested ? "Update check cancelled." : "Update check timed out. Try again later."; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or FormatException or KeyNotFoundException)
        { release = null; status = "Could not check for updates. Check your connection or try again later."; }
        finally { if (automatic) automaticRequest = null; SetBusy(false); }
    }

    public async Task InstallAsync()
    {
        if (busy || release is not { } available) return;
        SetBusy(true); status = "Downloading and verifying the release…"; Refresh();
        try { await install(available, lifetime.Token); status = "Download verified. Follow the installer or ZIP instructions."; }
        catch (OperationCanceledException) { status = "Download cancelled. PaneShift is still running."; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { status = "Update could not be completed. PaneShift remains available. Check the release page or try again."; }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool value) { busy = value; Refresh(); }
    private void Refresh()
    {
        Changed(nameof(IsBusy)); Changed(nameof(Status)); Changed(nameof(HasRelease)); Changed(nameof(ReleaseNotes));
        CheckCommand.Refresh(); ReleasePageCommand.Refresh(); InstallCommand.Refresh();
    }
    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
}
