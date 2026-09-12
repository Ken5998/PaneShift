using PaneShift.Core;

namespace PaneShift.Core.Tests;

public class GeometryTests
{
    [Theory]
    [InlineData(WindowAction.LeftHalf, 0, 0, 600, 600)]
    [InlineData(WindowAction.RightHalf, 600, 0, 600, 600)]
    [InlineData(WindowAction.TopHalf, 0, 0, 1200, 300)]
    [InlineData(WindowAction.BottomHalf, 0, 300, 1200, 300)]
    [InlineData(WindowAction.TopLeft, 0, 0, 600, 300)]
    [InlineData(WindowAction.TopRight, 600, 0, 600, 300)]
    [InlineData(WindowAction.BottomLeft, 0, 300, 600, 300)]
    [InlineData(WindowAction.BottomRight, 600, 300, 600, 300)]
    [InlineData(WindowAction.FirstThird, 0, 0, 400, 600)]
    [InlineData(WindowAction.CenterThird, 400, 0, 400, 600)]
    [InlineData(WindowAction.LastThird, 800, 0, 400, 600)]
    [InlineData(WindowAction.FirstTwoThirds, 0, 0, 800, 600)]
    [InlineData(WindowAction.CenterTwoThirds, 200, 0, 800, 600)]
    [InlineData(WindowAction.LastTwoThirds, 400, 0, 800, 600)]
    [InlineData(WindowAction.TopLeftSixth, 0, 0, 400, 300)]
    [InlineData(WindowAction.TopCenterSixth, 400, 0, 400, 300)]
    [InlineData(WindowAction.TopRightSixth, 800, 0, 400, 300)]
    [InlineData(WindowAction.BottomLeftSixth, 0, 300, 400, 300)]
    [InlineData(WindowAction.BottomCenterSixth, 400, 300, 400, 300)]
    [InlineData(WindowAction.BottomRightSixth, 800, 300, 400, 300)]
    public void NamedLayoutsOccupyExpectedRegion(WindowAction action, int x, int y, int width, int height)
    {
        Assert.Equal(new PixelRect(x - 1500, y - 800, width, height),
            WindowGeometry.Calculate(action, new(-1500, -800, 1200, 600)));
    }

    [Fact]
    public void LargeCoordinatesDoNotOverflowPartitionMultiplication()
    {
        var area = new PixelRect(-1000000000, 0, 2000000000, 1000);
        var last = WindowGeometry.Calculate(WindowAction.LastThird, area);
        Assert.Equal(area.Right, last.Right);
        Assert.Equal(666666667, last.Width);
    }

    [Fact]
    public void CenterPreservesSizeAndUsesWorkAreaOrigin() =>
        Assert.Equal(new PixelRect(-850, -250, 300, 200),
            WindowGeometry.Center(new(0, 0, 300, 200), new(-1200, -500, 1000, 700)));

    public static IEnumerable<object[]> WorkAreas()
    {
        foreach (var (width, height) in new[] { (1920, 1080), (2560, 1440), (3440, 1440), (3840, 1600), (5120, 1440), (1919, 1079) })
        foreach (var (x, y) in new[] { (0, 0), (120, 40), (-5120, -1600) })
            yield return [new PixelRect(x, y, width, height)];
    }

    [Theory, MemberData(nameof(WorkAreas))]
    public void ThirdsCoverWorkAreaExactly(PixelRect area)
    {
        var a = WindowGeometry.Calculate(WindowAction.FirstThird, area);
        var b = WindowGeometry.Calculate(WindowAction.CenterThird, area);
        var c = WindowGeometry.Calculate(WindowAction.LastThird, area);
        Assert.Equal(new PixelRect(area.X, area.Y, area.Width / 3, area.Height), a);
        Assert.Equal(a.Right, b.X);
        Assert.Equal(b.Right, c.X);
        Assert.Equal(area.Right, c.Right);
        Assert.Equal(area.Width, a.Width + b.Width + c.Width);
        Assert.InRange(Math.Abs(a.Width - c.Width), 0, 1);
    }

    [Theory, MemberData(nameof(WorkAreas))]
    public void AllPartitionsCoverWithoutOverlap(PixelRect area)
    {
        WindowAction[][] partitions =
        [
            [WindowAction.LeftHalf, WindowAction.RightHalf],
            [WindowAction.TopHalf, WindowAction.BottomHalf],
            [WindowAction.TopLeft, WindowAction.TopRight, WindowAction.BottomLeft, WindowAction.BottomRight],
            [WindowAction.TopLeftSixth, WindowAction.TopCenterSixth, WindowAction.TopRightSixth,
             WindowAction.BottomLeftSixth, WindowAction.BottomCenterSixth, WindowAction.BottomRightSixth]
        ];
        foreach (var partition in partitions)
        {
            var tiles = partition.Select(a => WindowGeometry.Calculate(a, area)).ToArray();
            Assert.Equal((long)area.Width * area.Height, tiles.Sum(t => (long)t.Width * t.Height));
            foreach (var tile in tiles)
            {
                Assert.InRange(tile.X, area.X, area.Right - 1);
                Assert.InRange(tile.Y, area.Y, area.Bottom - 1);
                Assert.InRange(tile.Right, area.X + 1, area.Right);
                Assert.InRange(tile.Bottom, area.Y + 1, area.Bottom);
            }
            for (int i = 0; i < tiles.Length; i++)
            for (int j = i + 1; j < tiles.Length; j++)
                Assert.True(tiles[i].Right <= tiles[j].X || tiles[j].Right <= tiles[i].X ||
                    tiles[i].Bottom <= tiles[j].Y || tiles[j].Bottom <= tiles[i].Y);
        }
    }

    [Theory, MemberData(nameof(WorkAreas))]
    public void TwoThirdsUseSharedBoundaries(PixelRect area)
    {
        var first = WindowGeometry.Calculate(WindowAction.FirstTwoThirds, area);
        var last = WindowGeometry.Calculate(WindowAction.LastTwoThirds, area);
        var center = WindowGeometry.Calculate(WindowAction.CenterTwoThirds, area);
        Assert.Equal(area.X, first.X);
        Assert.Equal(WindowGeometry.Calculate(WindowAction.LastThird, area).X, first.Right);
        Assert.Equal(WindowGeometry.Calculate(WindowAction.CenterThird, area).X, last.X);
        Assert.Equal(area.Right, last.Right);
        Assert.InRange(Math.Abs((center.X - area.X) - (area.Right - center.Right)), 0, 1);
        Assert.InRange(Math.Abs(center.Width - area.Width * 2L / 3), 0, 1);
    }

    [Fact]
    public void CenterClampsOversizedWindows() => Assert.Equal(new PixelRect(-100, 50, 900, 600),
        WindowGeometry.Center(new PixelRect(20, 20, 2000, 1000), new PixelRect(-100, 50, 900, 600)));

    [Fact]
    public void InvalidRectanglesAndUnsupportedActionsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelRect(0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PixelRect(0, 0, 1, -1));
        Assert.Throws<OverflowException>(() => new PixelRect(int.MaxValue, 0, 1, 1));
        Assert.Throws<NotSupportedException>(() => WindowGeometry.Calculate(WindowAction.Restore, new(0, 0, 100, 100)));
    }

    [Fact]
    public void HistoryRetainsOriginalStatePerHandle()
    {
        var history = new WindowHistory();
        var original = new WindowState(new(1, 2, 300, 400), true);
        history.Remember(1, original);
        history.Remember(1, new(new(0, 0, 100, 100), false));
        history.Remember(2, original);
        Assert.True(history.TryGet(1, out var saved));
        Assert.Equal(original, saved);
        history.Forget(1);
        Assert.False(history.TryGet(1, out _));
        Assert.True(history.TryGet(2, out _));
    }

    [Fact]
    public void DefaultsHaveEighteenUniqueShortcuts()
    {
        var bindings = PaneShiftConfiguration.Default.Hotkeys;
        Assert.Equal(18, bindings.Count);
        Assert.Equal(18, bindings.Select(b => (b.Modifiers, b.VirtualKey)).Distinct().Count());
    }

    [Fact]
    public void RightSixthsUseControlShiftWindowsArrowBindings()
    {
        var bindings = PaneShiftConfiguration.Default.Hotkeys;
        var modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows;
        Assert.Equal(0x000Eu, (uint)modifiers);
        Assert.Contains(new HotkeyBinding(modifiers, 0x26, WindowAction.TopRightSixth), bindings);
        Assert.Contains(new HotkeyBinding(modifiers, 0x28, WindowAction.BottomRightSixth), bindings);
        Assert.DoesNotContain(bindings, b => b.Action is WindowAction.TopLeftSixth or WindowAction.TopCenterSixth
            or WindowAction.BottomLeftSixth or WindowAction.BottomCenterSixth);
    }
}
