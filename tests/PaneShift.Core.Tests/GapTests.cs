using PaneShift.Core;

namespace PaneShift.Core.Tests;

public class GapTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var row in GeometryTests.WorkAreas())
        foreach (int gap in new[] { 0, 1, 10, 11, 20 })
        foreach (bool edges in new[] { false, true })
            yield return [row[0], gap, edges];
    }

    [Theory, MemberData(nameof(Cases))]
    public void AdjacentThirdsAndSixthsHaveExactVisibleGaps(PixelRect area, int gap, bool edges)
    {
        PixelRect Tile(WindowAction action) => WindowGaps.Apply(WindowGeometry.Calculate(action, area), area, gap, edges);
        var first = Tile(WindowAction.FirstThird);
        var center = Tile(WindowAction.CenterThird);
        var last = Tile(WindowAction.LastThird);
        Assert.Equal(gap, center.X - first.Right);
        Assert.Equal(gap, last.X - center.Right);
        Assert.Equal(gap, last.X - Tile(WindowAction.FirstTwoThirds).Right);
        Assert.Equal(gap, Tile(WindowAction.LastTwoThirds).X - first.Right);
        int outer = edges ? gap : 0;
        Assert.Equal(area.X + outer, first.X);
        Assert.Equal(area.Right - outer, last.Right);
        Assert.Equal(area.Y + outer, first.Y);
        Assert.Equal(area.Bottom - outer, first.Bottom);

        WindowAction[] top = [WindowAction.TopLeftSixth, WindowAction.TopCenterSixth, WindowAction.TopRightSixth];
        WindowAction[] bottom = [WindowAction.BottomLeftSixth, WindowAction.BottomCenterSixth, WindowAction.BottomRightSixth];
        for (int i = 0; i < 3; i++)
        {
            var a = Tile(top[i]);
            var b = Tile(bottom[i]);
            Assert.Equal(gap, b.Y - a.Bottom);
            Assert.Equal(a.X, b.X);
            Assert.Equal(a.Width, b.Width);
            Assert.Equal(area.Y + outer, a.Y);
            Assert.Equal(area.Bottom - outer, b.Bottom);
            if (i == 0) Assert.Equal(area.X + outer, a.X);
            if (i == 2) Assert.Equal(area.Right - outer, a.Right);
            if (i < 2)
            {
                Assert.Equal(gap, Tile(top[i + 1]).X - a.Right);
                Assert.Equal(gap, Tile(bottom[i + 1]).X - b.Right);
            }
        }
    }

    [Theory, MemberData(nameof(Cases))]
    public void AllLayoutsAndRepeatedSizesUseTheSameInsets(PixelRect area, int gap, bool edges)
    {
        var tiles = Enum.GetValues<WindowAction>().Where(a => a <= WindowAction.BottomRightSixth)
            .Select(a => WindowGeometry.Calculate(a, area)).Concat(
                Enum.GetValues<WindowAction>().Where(HalfActionCycle.AppliesTo)
                    .SelectMany(a => HalfActionCycle.Sizes.Select(s => WindowGeometry.CalculateHalf(a, area, s))));
        foreach (var tile in tiles)
        {
            var actual = WindowGaps.Apply(tile, area, gap, edges);
            int outer = edges ? gap : 0;
            Assert.Equal(tile.X + (tile.X == area.X ? outer : gap / 2), actual.X);
            Assert.Equal(tile.Y + (tile.Y == area.Y ? outer : gap / 2), actual.Y);
            Assert.Equal(tile.Right - (tile.Right == area.Right ? outer : gap - gap / 2), actual.Right);
            Assert.Equal(tile.Bottom - (tile.Bottom == area.Bottom ? outer : gap - gap / 2), actual.Bottom);
            if (gap == 0) Assert.Equal(tile, actual);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    [InlineData(int.MaxValue)]
    public void InvalidOrOversizedGapsAreRejected(int gap) => Assert.ThrowsAny<ArgumentException>(() =>
        WindowGaps.Apply(new(0, 0, 10, 10), new(0, 0, 10, 10), gap, true));

    [Fact]
    public void MinimumOnePixelTileIsAllowed()
    {
        Assert.Equal(new PixelRect(1, 1, 1, 1), WindowGaps.Apply(new(0, 0, 3, 3), new(0, 0, 3, 3), 1, true));
        Assert.Throws<ArgumentException>(() => WindowGaps.Apply(new(0, 0, 1, 3), new(0, 0, 3, 3), 1, false));
    }
}
