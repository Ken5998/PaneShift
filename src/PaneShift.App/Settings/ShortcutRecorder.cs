using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PaneShift.Core;

namespace PaneShift.App.Settings;

public sealed class ShortcutRecorder : System.Windows.Controls.Button
{
    public static readonly DependencyProperty ChordProperty = DependencyProperty.Register(nameof(Chord), typeof(string), typeof(ShortcutRecorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ShortcutRecorder)d).RefreshText()));
    public string? Chord { get => (string?)GetValue(ChordProperty); set => SetValue(ChordProperty, value); }
    public bool IsRecording { get; private set; }

    protected override void OnClick()
    {
        base.OnClick();
        Focus();
        IsRecording = true;
        RefreshText();
    }

    protected override void OnInitialized(EventArgs e) { base.OnInitialized(e); RefreshText(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { CancelRecording(); base.OnLostKeyboardFocus(e); }

    public void CancelRecording() { IsRecording = false; RefreshText(); }

    public void Accept(HotkeyChord chord)
    {
        if (!IsRecording) return;
        try
        {
            chord.Validate();
            SetCurrentValue(ChordProperty, chord.ToString());
            IsRecording = false;
            RefreshText();
        }
        catch (ArgumentException ex) { Content = "Try another shortcut…"; ToolTip = ex.Message; }
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (!IsRecording) { base.OnPreviewKeyDown(e); return; }
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { CancelRecording(); e.Handled = true; return; }
        if (key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None) { CancelRecording(); return; }
        e.Handled = true;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (!e.IsRepeat) Accept(new((HotkeyModifiers)Keyboard.Modifiers, (uint)KeyInterop.VirtualKeyFromKey(key)));
    }

    private void RefreshText()
    {
        Content = IsRecording ? "Press shortcut…" : Chord is null ? "Not set" : HotkeyChord.Parse(Chord).Display;
        ToolTip = IsRecording ? "Press a modifier and a key. Escape cancels; Tab moves to the next control." : "Click or press Space to record a shortcut";
    }
}
