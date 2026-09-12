namespace PaneShift.Core;

public static class WindowGaps
{
    /// <summary>Insets an ideal visible-frame tile once, using complementary insets at shared edges.</summary>
    public static PixelRect Apply(PixelRect tile, PixelRect workArea, int gapPixels, bool applyToScreenEdges)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gapPixels);
        if (tile.Width <= 0 || tile.Height <= 0 || tile.X < workArea.X || tile.Y < workArea.Y ||
            tile.Right > workArea.Right || tile.Bottom > workArea.Bottom)
            throw new ArgumentException("The tile must be a positive rectangle inside the work area.", nameof(tile));
        if (gapPixels == 0) return tile;
        int leading = gapPixels / 2;
        int trailing = gapPixels - leading;
        int outer = applyToScreenEdges ? gapPixels : 0;
        int left = tile.X == workArea.X ? outer : leading;
        int top = tile.Y == workArea.Y ? outer : leading;
        int right = tile.Right == workArea.Right ? outer : trailing;
        int bottom = tile.Bottom == workArea.Bottom ? outer : trailing;
        long width = (long)tile.Width - left - right;
        long height = (long)tile.Height - top - bottom;
        if (width <= 0 || height <= 0)
            throw new ArgumentException("The configured gap is too large for this tile. Reduce gapPixels.", nameof(gapPixels));
        return new(tile.X + left, tile.Y + top, (int)width, (int)height);
    }
}
