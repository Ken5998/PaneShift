namespace PaneShift.Core;

public static class WindowGeometry
{
    public static PixelRect Calculate(WindowAction action, PixelRect workArea)
    {
        // Rational boundaries share the same rounding, so neighboring tiles meet exactly.
        var (left, top, right, bottom, columns, rows) = action switch
        {
            WindowAction.LeftHalf => (0, 0, 1, 1, 2, 1),
            WindowAction.RightHalf => (1, 0, 2, 1, 2, 1),
            WindowAction.TopHalf => (0, 0, 1, 1, 1, 2),
            WindowAction.BottomHalf => (0, 1, 1, 2, 1, 2),
            WindowAction.TopLeft => (0, 0, 1, 1, 2, 2),
            WindowAction.TopRight => (1, 0, 2, 1, 2, 2),
            WindowAction.BottomLeft => (0, 1, 1, 2, 2, 2),
            WindowAction.BottomRight => (1, 1, 2, 2, 2, 2),
            WindowAction.FirstThird => (0, 0, 1, 1, 3, 1),
            WindowAction.CenterThird => (1, 0, 2, 1, 3, 1),
            WindowAction.LastThird => (2, 0, 3, 1, 3, 1),
            WindowAction.FirstTwoThirds => (0, 0, 2, 1, 3, 1),
            WindowAction.CenterTwoThirds => (1, 0, 5, 1, 6, 1),
            WindowAction.LastTwoThirds => (1, 0, 3, 1, 3, 1),
            WindowAction.TopLeftSixth => (0, 0, 1, 1, 3, 2),
            WindowAction.TopCenterSixth => (1, 0, 2, 1, 3, 2),
            WindowAction.TopRightSixth => (2, 0, 3, 1, 3, 2),
            WindowAction.BottomLeftSixth => (0, 1, 1, 2, 3, 2),
            WindowAction.BottomCenterSixth => (1, 1, 2, 2, 3, 2),
            WindowAction.BottomRightSixth => (2, 1, 3, 2, 3, 2),
            _ => throw new NotSupportedException($"{action} is not a tiled layout.")
        };
        int x1 = Boundary(workArea.Width, left, columns);
        int x2 = Boundary(workArea.Width, right, columns);
        int y1 = Boundary(workArea.Height, top, rows);
        int y2 = Boundary(workArea.Height, bottom, rows);
        return new(workArea.X + x1, workArea.Y + y1, x2 - x1, y2 - y1);
    }

    public static PixelRect Center(PixelRect window, PixelRect workArea)
    {
        int width = Math.Min(window.Width, workArea.Width);
        int height = Math.Min(window.Height, workArea.Height);
        return new(workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2, width, height);
    }

    private static int Boundary(int size, int numerator, int denominator) =>
        (int)((long)size * numerator / denominator);
}
