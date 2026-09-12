using System.Windows;
using System.Windows.Media;
using PaneShift.Core;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;

namespace PaneShift.App.Settings;

public sealed class LayoutGlyph : FrameworkElement
{
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(nameof(Action), typeof(WindowAction), typeof(LayoutGlyph), new FrameworkPropertyMetadata(WindowAction.LeftHalf, FrameworkPropertyMetadataOptions.AffectsRender));
    public WindowAction Action { get => (WindowAction)GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var area = new PixelRect(0, 0, 120, 72);
        var tile = Action switch
        {
            WindowAction.Maximize => area,
            WindowAction.Center => WindowGeometry.Center(new(0, 0, 70, 44), area),
            WindowAction.Restore => new PixelRect(12, 12, 72, 44),
            _ => WindowGeometry.Calculate(Action, area)
        };
        double sx = (ActualWidth - 4) / area.Width, sy = (ActualHeight - 4) / area.Height;
        var stroke = (Brush)FindResource("StrokeBrush");
        dc.DrawRoundedRectangle((Brush)FindResource("CanvasBrush"), new Pen(stroke, 1), new(1, 1, ActualWidth - 2, ActualHeight - 2), 4, 4);
        dc.DrawRoundedRectangle((Brush)FindResource("AccentBrush"), null,
            new(2 + tile.X * sx, 2 + tile.Y * sy, tile.Width * sx, tile.Height * sy), 2, 2);
    }
}

public sealed class LayoutPreview : FrameworkElement
{
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(nameof(Gap), typeof(double), typeof(LayoutPreview), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty EdgesProperty = DependencyProperty.Register(nameof(Edges), typeof(bool), typeof(LayoutPreview), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Gap { get => (double)GetValue(GapProperty); set => SetValue(GapProperty, value); }
    public bool Edges { get => (bool)GetValue(EdgesProperty); set => SetValue(EdgesProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var area = new PixelRect(0, 0, 600, 240);
        double sx = (ActualWidth - 4) / area.Width, sy = (ActualHeight - 4) / area.Height;
        dc.DrawRoundedRectangle((Brush)FindResource("CanvasBrush"), new Pen((Brush)FindResource("StrokeBrush"), 1), new(1, 1, ActualWidth - 2, ActualHeight - 2), 8, 8);
        foreach (var action in new[] { WindowAction.FirstTwoThirds, WindowAction.LastThird })
        {
            var tile = WindowGaps.Apply(WindowGeometry.Calculate(action, area), area, (int)Math.Clamp(Gap, 0, 64), Edges);
            dc.DrawRoundedRectangle((Brush)FindResource(action == WindowAction.FirstTwoThirds ? "AccentBrush" : "PreviewBrush"), null,
                new(2 + tile.X * sx, 2 + tile.Y * sy, tile.Width * sx, tile.Height * sy), 4, 4);
        }
    }
}
