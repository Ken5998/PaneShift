using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PaneShift.App;

/// <summary>Owns one icon shared by the tray and future WPF windows.</summary>
internal sealed class ApplicationIcon : IDisposable
{
    public Icon TrayIcon { get; }
    public string? Warning { get; }
    private ImageSource? windowIcon;

    public ApplicationIcon()
    {
        try
        {
            using var stream = typeof(ApplicationIcon).Assembly.GetManifestResourceStream("PaneShift.Icon");
            if (stream is not null)
            {
                using var source = new Icon(stream);
                TrayIcon = (Icon)source.Clone();
                return;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            Warning = $"Could not load the PaneShift icon; using the default icon. {ex.Message}";
        }
        TrayIcon = (Icon)SystemIcons.Application.Clone();
    }

    public ImageSource WindowIcon
    {
        get
        {
            if (windowIcon is null)
            {
                windowIcon = Imaging.CreateBitmapSourceFromHIcon(TrayIcon.Handle, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                windowIcon.Freeze();
            }
            return windowIcon;
        }
    }

    public void Dispose() => TrayIcon.Dispose();
}
