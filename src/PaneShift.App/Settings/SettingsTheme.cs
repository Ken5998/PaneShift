using System.Windows;
using System.Windows.Media;
using PaneShift.Windows;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SystemColors = System.Windows.SystemColors;

namespace PaneShift.App.Settings;

internal static class SettingsTheme
{
    public static void Apply(Window window)
    {
        bool dark = ApplicationAppearance.UsesDarkTheme();
        var colors = new Dictionary<string, string>
        {
            ["CanvasBrush"] = dark ? "#171B23" : "#F3F5F9",
            ["CardBrush"] = dark ? "#222733" : "#FFFFFF",
            ["TextBrush"] = dark ? "#EDF1FA" : "#1E293B",
            ["MutedBrush"] = dark ? "#AEB9CE" : "#52627A",
            ["StrokeBrush"] = dark ? "#3C4659" : "#CCD5E3",
            ["AccentBrush"] = dark ? "#60A5FA" : "#2563EB",
            ["PreviewBrush"] = dark ? "#3D718B" : "#71B7D8",
            ["ErrorBrush"] = dark ? "#FFB4AB" : "#A12222"
        };
        foreach (var (key, color) in colors) window.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        if (SystemParameters.HighContrast)
        {
            window.Resources["CanvasBrush"] = window.Resources["CardBrush"] = SystemColors.WindowBrush;
            window.Resources["TextBrush"] = window.Resources["MutedBrush"] = window.Resources["StrokeBrush"] = SystemColors.WindowTextBrush;
            window.Resources["AccentBrush"] = window.Resources["PreviewBrush"] = SystemColors.HighlightBrush;
            window.Resources["ErrorBrush"] = SystemColors.WindowTextBrush;
        }
    }
}
