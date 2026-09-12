using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PaneShift.Core;

namespace PaneShift.App.Settings;

public partial class SettingsWindow : Window
{
    private readonly Action openFile, openFolder, openRepository, elevate;
    private bool discardOnClose;
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel model, ImageSource icon, string path, bool? elevated,
        Action openFile, Action openFolder, Action openRepository, Action elevate)
    {
        ViewModel = model;
        this.openFile = openFile; this.openFolder = openFolder;
        this.openRepository = openRepository; this.elevate = elevate;
        InitializeComponent();
        SettingsTheme.Apply(this);
        DataContext = model;
        Activated += (_, _) => ViewModel.LoginStartup?.Refresh();
        Icon = BrandIcon.Source = icon;
        ConfigPath.Text = path;
        var version = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(SettingsWindow).Assembly)?.InformationalVersion;
        VersionLabel.Text = $"Version {version} · MIT License";
        ElevateButton.Visibility = elevated == false ? Visibility.Visible : Visibility.Collapsed;
        ElevationHint.Text = elevated == true
            ? "To run normally again, Exit PaneShift and launch it from a standard Explorer session."
            : "Administrator mode is only needed to manage elevated windows. Restarting asks Windows for permission.";
    }

    public bool ConfirmPendingChanges()
    {
        if (!ViewModel.HasChanges) return true;
        var result = System.Windows.MessageBox.Show(this,
            "Apply your changes before closing?\n\nYes: apply changes. No: discard edits. Cancel: keep editing.",
            "PaneShift — Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result == MessageBoxResult.No || (result == MessageBoxResult.Yes && ViewModel.Apply());
    }

    public void CloseForExit() { discardOnClose = true; Close(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!discardOnClose) e.Cancel = !ConfirmPendingChanges(); }
    private void Cancel(object sender, RoutedEventArgs e) { discardOnClose = true; Close(); }
    private void ResetShortcuts(object sender, RoutedEventArgs e) => ViewModel.ResetShortcuts();
    private void RestoreDefaults(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(this, "Replace the draft with defaults? Changes take effect only after Apply.",
            "PaneShift", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) ViewModel.RestoreDefaults();
    }
    private void ReloadDraft(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(this, "Discard these edits and load the active settings into this window?", "PaneShift",
            MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) ViewModel.ReloadDraft();
    }
    private void OpenFile(object sender, RoutedEventArgs e) => openFile();
    private void OpenFolder(object sender, RoutedEventArgs e) => openFolder();
    private void OpenRepository(object sender, RoutedEventArgs e) => openRepository();
    private void Elevate(object sender, RoutedEventArgs e) => elevate();

    // Windows consumes registered chords before WPF sees KeyDown. Forward our own
    // WM_HOTKEY only to the focused recorder; other windows retain normal behavior.
    public bool TryRecordHotkey(HotkeyBinding binding)
    {
        if (!IsActive || Keyboard.FocusedElement is not ShortcutRecorder { IsRecording: true } recorder) return false;
        recorder.Accept(new(binding.Modifiers, binding.VirtualKey));
        return true;
    }
}
