using PaneShift.Core;

namespace PaneShift.Core.Tests;

public class RepetitionTests
{
    [Fact]
    public void ConsecutiveSameTargetAndActionWrapAround()
    {
        var repetition = new CommandRepetition();
        Assert.Equal(new[] { 0, 1, 2, 0, 1, 2, 0 },
            Enumerable.Range(0, 7).Select(_ => repetition.Next(42, WindowAction.LeftHalf, 3)));
        Assert.Equal((nint)42, repetition.Target);
        Assert.Equal(WindowAction.LeftHalf, repetition.Action);
    }

    [Fact]
    public void ChangedActionTargetRestoreInvalidTargetAndExplicitResetRestartSequence()
    {
        var repetition = new CommandRepetition();
        repetition.Next(1, WindowAction.LeftHalf, 3);
        Assert.Equal(0, repetition.Next(2, WindowAction.LeftHalf, 3));
        Assert.Equal(0, repetition.Next(1, WindowAction.LeftHalf, 3));
        Assert.Equal(0, repetition.Next(1, WindowAction.RightHalf, 3));
        Assert.Equal(0, repetition.Next(1, WindowAction.LeftHalf, 3));
        repetition.Next(1, WindowAction.Center, 1);
        Assert.Equal(0, repetition.Next(1, WindowAction.LeftHalf, 3));
        repetition.Next(1, WindowAction.Restore, 3);
        Assert.Equal(0, repetition.Next(1, WindowAction.LeftHalf, 3));
        repetition.Next(0, WindowAction.LeftHalf, 3);
        Assert.Equal(0, repetition.Next(1, WindowAction.LeftHalf, 3));
        repetition.Reset();
        Assert.Equal(0, repetition.Next(1, WindowAction.LeftHalf, 3));
    }

    public static IEnumerable<object[]> RepeatedLayouts()
    {
        foreach (int size in new[] { 1200, 1201, 1202 })
        foreach (var action in new[] { WindowAction.LeftHalf, WindowAction.RightHalf, WindowAction.TopHalf, WindowAction.BottomHalf })
        foreach (var halfSize in HalfActionCycle.Sizes)
            yield return [size, action, halfSize];
    }

    [Theory, MemberData(nameof(RepeatedLayouts))]
    public void RepeatedSizesRemainAnchoredWithSharedRounding(int dimension, WindowAction action, HalfSize size)
    {
        var area = new PixelRect(-2200, -1500, dimension, dimension);
        var tile = WindowGeometry.CalculateHalf(action, area, size);
        bool trailing = action is WindowAction.RightHalf or WindowAction.BottomHalf;
        int expectedSize = size switch
        {
            HalfSize.Half => trailing ? dimension - dimension / 2 : dimension / 2,
            HalfSize.TwoThirds => trailing ? dimension - dimension / 3 : dimension * 2 / 3,
            _ => trailing ? dimension - dimension * 2 / 3 : dimension / 3
        };
        var expected = action switch
        {
            WindowAction.LeftHalf => new PixelRect(area.X, area.Y, expectedSize, dimension),
            WindowAction.RightHalf => new PixelRect(area.Right - expectedSize, area.Y, expectedSize, dimension),
            WindowAction.TopHalf => new PixelRect(area.X, area.Y, dimension, expectedSize),
            _ => new PixelRect(area.X, area.Bottom - expectedSize, dimension, expectedSize)
        };
        Assert.Equal(expected, tile);
    }
}
