namespace PaneShift.Core;

/// <summary>A rectangle in physical desktop pixels; origins may be negative.</summary>
public readonly record struct PixelRect
{
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
    public int Right => checked(X + Width);
    public int Bottom => checked(Y + Height);

    public PixelRect(int x, int y, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        _ = checked(x + width);
        _ = checked(y + height);
        (X, Y, Width, Height) = (x, y, width, height);
    }
}
