using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Interop;
using PaneShift.Core;
using PaneShift.Windows;
using PaneShift.App.Settings;
using Forms = System.Windows.Forms;

namespace PaneShift.App;

public partial class App : System.Windows.Application
{
    private readonly RuntimeSettings runtime = new();
    private readonly WindowService windows;
    private SettingsStore? settingsStore;
    private string? settingsWarning;
    private ConfigurationActivation? activation;
    private SettingsWindow? settingsWindow;
    private LoginStartupViewModel? loginStartup;
    private Mutex? instance;
    private bool ownsMutex;
    private HwndSource? source;
    private GlobalHotkeys? hotkeys;
    private Forms.NotifyIcon? tray;
    private ApplicationIcon? applicationIcon;
    private Forms.ContextMenuStrip? menu;
    private IReadOnlyList<HotkeyRegistrationFailure> failures = [];
    private bool paused;
    private bool? isElevated;
    private string? privilegeWarning;
    private string? lastCommandFailure;
    private string? restartDiagnostic;
    private bool restartInProgress;

    public App() => windows = new WindowService(runtime);

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        RestartRequest? restart;
        try
        {
            restart = RestartRequest.Parse(e.Args);
            if (restart is not null)
            {
                ElevatedRestart.WaitForPreviousInstance(restart);
                paused = restart.Paused;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or Win32Exception or InvalidOperationException or TimeoutException)
        {
            System.Windows.MessageBox.Show($"PaneShift could not complete the restart. {ex.Message}", "PaneShift");
            Shutdown(1);
            return;
        }
        try
        {
            instance = new Mutex(false, @"Local\PaneShift");
            try { ownsMutex = instance.WaitOne(0); }
            catch (AbandonedMutexException) { ownsMutex = true; }
        }
        catch (UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(
                "PaneShift could not acquire its single-instance lock. It may already be running as administrator. " +
                "Exit that instance before starting PaneShift normally.", "PaneShift");
            Shutdown(1);
            return;
        }
        if (!ownsMutex)
        {
            System.Windows.MessageBox.Show("PaneShift is already running in the notification area.", "PaneShift");
            Shutdown();
            return;
        }
        try
        {
            try { isElevated = ProcessPrivileges.IsCurrentProcessElevated; }
            catch (Win32Exception ex) { privilegeWarning = $"Could not query process elevation. Win32 error {ex.NativeErrorCode}: {ex.Message}"; }
            string startupExecutable = Path.Combine(AppContext.BaseDirectory, "PaneShift.App.exe");
            string? startupUnavailable = isElevated != false
                ? "Change startup from a standard (non-administrator) PaneShift instance for your account."
                : !File.Exists(startupExecutable) ? "Run an installed or extracted PaneShift executable to configure startup." : null;
#if DEBUG
            startupUnavailable = "Startup is unavailable in Debug builds. Use the installed or published Release application.";
#endif
            loginStartup = new LoginStartupViewModel(new LoginStartup(new RegistryLoginStartupStore(), startupExecutable, startupUnavailable));
            settingsStore = new SettingsStore(restart?.SettingsFile ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaneShift", "settings.json"));
            var loaded = settingsStore.LoadOrCreate();
            settingsWarning = loaded.Warning;
            runtime.ReplaceCurrent(loaded.Settings);
            source = new HwndSource(new HwndSourceParameters("PaneShift.Hotkeys")
            {
                ParentWindow = new nint(-3), // HWND_MESSAGE: no visible or focusable window.
                WindowStyle = 0
            });
            source.AddHook(OnMessage);
            hotkeys = new GlobalHotkeys(source.Handle);
            activation = new ConfigurationActivation(runtime, settingsStore, hotkeys);
            menu = new Forms.ContextMenuStrip();
            menu.Items.Add(new Forms.ToolStripMenuItem("PaneShift") { Enabled = false });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
            menu.Items.Add("Shortcuts and status", null, (_, _) => ShowStatus());
            menu.Items.Add("Open Settings File", null, (_, _) => OpenSettingsFile());
            menu.Items.Add("Reload Settings", null, (_, _) => ReloadSettings());
            menu.Items.Add(new Forms.ToolStripSeparator());
            var pause = new Forms.ToolStripMenuItem("Pause shortcuts") { CheckOnClick = true, Checked = paused };
            pause.Click += (_, _) =>
            {
                paused = pause.Checked;
                windows.ResetRepetition();
                if (paused) hotkeys?.ReleaseAll();
                else RegisterShortcuts();
                if (tray is not null) tray.Text = paused ? "PaneShift — paused" : "PaneShift";
                RefreshSettingsStatus();
            };
            menu.Items.Add(pause);
            var startupItem = new Forms.ToolStripMenuItem("Start PaneShift when I sign in");
            void RefreshStartupItem()
            {
                startupItem.Checked = loginStartup.IsEnabled;
                startupItem.Enabled = loginStartup.CanChange;
                startupItem.ToolTipText = loginStartup.Description;
            }
            loginStartup.PropertyChanged += (_, _) => RefreshStartupItem();
            menu.Opening += (_, _) => loginStartup.Refresh();
            startupItem.Click += (_, _) =>
            {
                loginStartup.IsEnabled = !loginStartup.IsEnabled;
                if (loginStartup.Error.Length > 0) Notify(loginStartup.Error);
            };
            RefreshStartupItem();
            menu.Items.Add(startupItem);
            menu.Items.Add(new Forms.ToolStripSeparator());
            if (isElevated == false)
                menu.Items.Add("Restart as administrator...", null, (_, _) => RestartAsAdministrator());
            else if (isElevated == true)
                menu.Items.Add(new Forms.ToolStripMenuItem("Running as administrator") { Enabled = false });
            menu.Items.Add("Exit", null, (_, _) => ExitFromTray());
            applicationIcon = new ApplicationIcon();
            tray = new Forms.NotifyIcon
            {
                Icon = applicationIcon.TrayIcon,
                Text = paused ? "PaneShift — paused" : "PaneShift",
                ContextMenuStrip = menu,
                Visible = true
            };
            tray.DoubleClick += (_, _) => OpenSettings();
            if (!paused) RegisterShortcuts();
            if (settingsWarning is not null) Notify(settingsWarning);
            if (applicationIcon.Warning is not null) Notify(applicationIcon.Warning);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            System.Windows.MessageBox.Show($"PaneShift could not start: {ex.Message}", "PaneShift");
            Shutdown(1);
        }
    }

    private void RegisterShortcuts()
    {
        failures = hotkeys!.Register(HotkeySettings.Resolve(runtime.Current));
        if (failures.Count > 0)
            Notify($"{failures.Count} shortcut(s) could not be registered. Open Shortcuts and status for details.");
    }

    private nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != GlobalHotkeys.HotkeyMessage || paused || hotkeys is null ||
            !hotkeys.TryGetBinding((int)wParam, out var binding) || binding is null) return 0;
        handled = true;
        if (settingsWindow?.TryRecordHotkey(binding) == true) return 0;
        var action = binding.Action;
        var failure = windows.Execute(action);
        if (failure is not null)
        {
            lastCommandFailure = $"{action}: {failure.Details}";
            string notification = failure.Message;
            if (failure.Kind == WindowFailureKind.AccessDenied && isElevated != false)
                notification = "Windows denied access to this window. PaneShift cannot control this protected window with its current privileges.";
            Notify(notification);
        }
        return 0;
    }

    private void ShowStatus()
    {
        string text = paused ? "Shortcuts are paused.\n\n" : "PaneShift is running.\n\n";
        text += $"Privilege level: {(isElevated is true ? "Administrator" : isElevated is false ? "Standard" : "Unknown")}\n\n";
        text += string.Join("\n", HotkeySettings.Resolve(runtime.Current).Select(binding =>
            $"{FormatShortcut(binding)} — {binding.Action}"));
        if (failures.Count > 0)
            text += "\n\nRegistration failures:\n" + string.Join("\n", failures.Select(f =>
                $"{FormatShortcut(f.Binding)} — {f.Binding.Action}: {f.Reason}"));
        if (settingsWarning is not null) text += "\n\n" + settingsWarning;
        if (hotkeys?.CleanupWarning is { } cleanup) text += "\n\n" + cleanup;
        if (privilegeWarning is not null) text += "\n\n" + privilegeWarning;
        if (lastCommandFailure is not null) text += "\n\nLast window command failure:\n" + lastCommandFailure;
        if (restartDiagnostic is not null) text += "\n\nLast restart attempt:\n" + restartDiagnostic;
        if (isElevated == true) text += "\n\nTo run normally again, choose Exit and launch PaneShift from a standard Explorer session.";
        if (applicationIcon?.Warning is { } iconWarning) text += "\n\n" + iconWarning;
        text += "\n\nActive settings:\n" + DescribeSettings();
        text += "\n\nAfter saving settings.json, choose Reload Settings from the tray.";
        System.Windows.MessageBox.Show(text, "PaneShift — Shortcuts and status");
    }

    private void Notify(string message) => tray?.ShowBalloonTip(6000, "PaneShift", message, Forms.ToolTipIcon.Warning);

    private void RestartAsAdministrator()
    {
        if (restartInProgress || settingsStore is null || isElevated != false) return;
        if (settingsWindow?.ConfirmPendingChanges() == false) return;
        restartInProgress = true;
        try
        {
            // Always restart our apphost, including when this instance was launched via dotnet run.
            string executable = Path.Combine(AppContext.BaseDirectory, "PaneShift.App.exe");
            var result = ElevatedRestart.Start(executable, settingsStore.FilePath, paused);
            restartDiagnostic = result.Details;
            if (result.Outcome == RestartOutcome.Started)
            {
                // OnExit releases hotkeys, icon and mutex. The replacement waits for process exit.
                settingsWindow?.CloseForExit();
                Shutdown();
            }
            else if (result.Outcome == RestartOutcome.Cancelled)
                tray?.ShowBalloonTip(4000, "PaneShift", "Administrator restart cancelled. PaneShift is still running normally.", Forms.ToolTipIcon.Info);
            else
                Notify("Could not restart as administrator. PaneShift is still running. See Shortcuts and status for details.");
        }
        finally { restartInProgress = false; }
    }

    private void ReloadSettings()
    {
        if (activation is null) return;
        settingsWarning = activation.Reload(paused);
        if (settingsWarning is not null)
        {
            tray?.ShowBalloonTip(6000, "PaneShift — Settings reload failed",
                "Could not load settings.json. Existing settings remain active. See Shortcuts and status for details.",
                Forms.ToolTipIcon.Error);
            return;
        }
        failures = [];
        settingsWindow?.ViewModel.ExternalReload(runtime.Current);
        RefreshSettingsStatus();
        tray?.ShowBalloonTip(4000, "PaneShift — Settings reloaded", DescribeSettings(), Forms.ToolTipIcon.Info);
    }

    private void OpenSettings()
    {
        if (settingsWindow is null)
        {
            loginStartup?.Refresh();
            var model = new SettingsViewModel(runtime.Current, ApplySettings, loginStartup);
            settingsWindow = new SettingsWindow(model, applicationIcon!.WindowIcon, settingsStore!.FilePath, isElevated,
                () => OpenPath(settingsStore.FilePath), () => OpenPath(Path.GetDirectoryName(settingsStore.FilePath)!),
                () => OpenPath("https://github.com/Ken5998/PaneShift"), RestartAsAdministrator);
            settingsWindow.Closed += (_, _) => settingsWindow = null;
            RefreshSettingsStatus();
            settingsWindow.Show();
        }
        if (settingsWindow.WindowState == System.Windows.WindowState.Minimized)
            settingsWindow.WindowState = System.Windows.WindowState.Normal;
        settingsWindow.Activate();
    }

    private string? ApplySettings(PaneShiftSettings candidate)
    {
        var error = activation!.Apply(candidate, persist: true, paused);
        if (error is null) { settingsWarning = null; failures = []; }
        RefreshSettingsStatus();
        return error;
    }

    private void RefreshSettingsStatus()
    {
        if (settingsWindow is null) return;
        settingsWindow.ViewModel.RuntimeStatus =
            $"Shortcut status: {(paused ? "Paused" : "Active")}\nPrivilege level: {(isElevated is true ? "Administrator" : isElevated is false ? "Standard" : "Unknown")}" +
            (failures.Count > 0 ? $"\n{failures.Count} shortcut(s) unavailable. See Shortcuts and status in the tray." : "") +
            (hotkeys?.CleanupWarning is { } warning ? $"\n{warning}" : "");
    }

    private void ExitFromTray()
    {
        if (settingsWindow?.ConfirmPendingChanges() == false) return;
        settingsWindow?.CloseForExit();
        Shutdown();
    }

    private void OpenPath(string path)
    {
        try { using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        { Notify($"Could not open {path}: {ex.Message}"); }
    }

    private string DescribeSettings()
    {
        var settings = runtime.Current;
        return $"Gap: {settings.GapPixels} px\nScreen-edge gaps: {(settings.ApplyGapToScreenEdges ? "On" : "Off")}\nHalf repeat: Cycle sizes";
    }

    private static string FormatShortcut(HotkeyBinding binding)
    {
        var parts = new List<string>(5);
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        parts.Add(((Forms.Keys)binding.VirtualKey).ToString());
        return string.Join("+", parts);
    }

    private void OpenSettingsFile()
    {
        if (settingsStore is null) return;
        try
        {
            // If the file was deleted during this session, recreate defaults on explicit request.
            if (!File.Exists(settingsStore.FilePath))
            {
                var loaded = settingsStore.LoadOrCreate();
                if (loaded.Warning is not null) { Notify(loaded.Warning); return; }
            }
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            start.Arguments = $"/select,\"{settingsStore.FilePath}\"";
            using var process = Process.Start(start);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Notify($"Could not reveal settings: {ex.Message}");
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        hotkeys?.Dispose();
        if (source is not null) { source.RemoveHook(OnMessage); source.Dispose(); }
        tray?.Dispose();
        applicationIcon?.Dispose();
        menu?.Dispose();
        if (ownsMutex) instance?.ReleaseMutex();
        instance?.Dispose();
        base.OnExit(e);
    }
}
