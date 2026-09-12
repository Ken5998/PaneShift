using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Interop;
using PaneShift.Core;
using PaneShift.Windows;
using Forms = System.Windows.Forms;

namespace PaneShift.App;

public partial class App : System.Windows.Application
{
    private WindowService windows = new();
    private SettingsStore? settingsStore;
    private string? settingsWarning;
    private readonly PaneShiftConfiguration configuration = PaneShiftConfiguration.Default;
    private Mutex? instance;
    private bool ownsMutex;
    private HwndSource? source;
    private GlobalHotkeys? hotkeys;
    private Forms.NotifyIcon? tray;
    private ApplicationIcon? applicationIcon;
    private Forms.ContextMenuStrip? menu;
    private IReadOnlyList<HotkeyRegistrationFailure> failures = [];
    private bool paused;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, @"Local\PaneShift", out ownsMutex);
        if (!ownsMutex)
        {
            System.Windows.MessageBox.Show("PaneShift is already running in the notification area.", "PaneShift");
            Shutdown();
            return;
        }
        try
        {
            settingsStore = new SettingsStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaneShift", "settings.json"));
            var loaded = settingsStore.LoadOrCreate();
            settingsWarning = loaded.Warning;
            windows = new WindowService(loaded.Settings);
            source = new HwndSource(new HwndSourceParameters("PaneShift.Hotkeys")
            {
                ParentWindow = new nint(-3), // HWND_MESSAGE: no visible or focusable window.
                WindowStyle = 0
            });
            source.AddHook(OnMessage);
            menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Shortcuts and status", null, (_, _) => ShowStatus());
            menu.Items.Add("Open Settings File", null, (_, _) => OpenSettingsFile());
            var pause = new Forms.ToolStripMenuItem("Pause shortcuts") { CheckOnClick = true };
            pause.Click += (_, _) =>
            {
                paused = pause.Checked;
                windows.ResetRepetition();
                if (paused) { hotkeys?.Dispose(); hotkeys = null; }
                else RegisterShortcuts();
                if (tray is not null) tray.Text = paused ? "PaneShift — paused" : "PaneShift";
            };
            menu.Items.Add(pause);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => Shutdown());
            applicationIcon = new ApplicationIcon();
            tray = new Forms.NotifyIcon
            {
                Icon = applicationIcon.TrayIcon,
                Text = "PaneShift",
                ContextMenuStrip = menu,
                Visible = true
            };
            tray.DoubleClick += (_, _) => ShowStatus();
            RegisterShortcuts();
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
        hotkeys = new GlobalHotkeys(source!.Handle);
        failures = hotkeys.Register(configuration.Hotkeys);
        if (failures.Count > 0)
            Notify($"{failures.Count} shortcut(s) could not be registered. Open Shortcuts and status for details.");
    }

    private nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != GlobalHotkeys.HotkeyMessage || paused || hotkeys is null ||
            !hotkeys.TryGetAction((int)wParam, out var action)) return 0;
        handled = true;
        try { windows.Execute(action); }
        catch (Exception ex) when (ex is Win32Exception or ArgumentException or NotSupportedException or OverflowException)
        {
            Notify($"{action} failed: {ex.Message}");
        }
        return 0;
    }

    private void ShowStatus()
    {
        string text = paused ? "Shortcuts are paused.\n\n" : "PaneShift is running.\n\n";
        text += string.Join("\n", configuration.Hotkeys.Select(binding =>
            $"{FormatShortcut(binding)} — {binding.Action}"));
        if (failures.Count > 0)
            text += "\n\nRegistration failures:\n" + string.Join("\n", failures.Select(f =>
                $"{FormatShortcut(f.Binding)} — {f.Binding.Action}: {f.Reason}"));
        if (settingsWarning is not null) text += "\n\n" + settingsWarning;
        if (applicationIcon?.Warning is { } iconWarning) text += "\n\n" + iconWarning;
        text += "\n\nSettings changes take effect after restarting PaneShift.";
        System.Windows.MessageBox.Show(text, "PaneShift — Shortcuts and status");
    }

    private void Notify(string message) => tray?.ShowBalloonTip(6000, "PaneShift", message, Forms.ToolTipIcon.Warning);

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
