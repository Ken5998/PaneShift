namespace PaneShift.Core;

/// <summary>Maps repeated tiled actions to the other horizontal positions of the same size and row.</summary>
public static class PositionActionCycle
{
    public const int Length = 3;

    public static bool AppliesTo(WindowAction action) => action is
        WindowAction.FirstThird or WindowAction.CenterThird or WindowAction.LastThird or
        WindowAction.FirstTwoThirds or WindowAction.CenterTwoThirds or WindowAction.LastTwoThirds or
        WindowAction.TopLeftSixth or WindowAction.TopCenterSixth or WindowAction.TopRightSixth or
        WindowAction.BottomLeftSixth or WindowAction.BottomCenterSixth or WindowAction.BottomRightSixth;

    public static WindowAction At(WindowAction action, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= Length) throw new ArgumentOutOfRangeException(nameof(index));

        return action switch
        {
            WindowAction.FirstThird => Forward(index, WindowAction.FirstThird, WindowAction.CenterThird, WindowAction.LastThird),
            WindowAction.CenterThird => Forward(index, WindowAction.CenterThird, WindowAction.LastThird, WindowAction.FirstThird),
            WindowAction.LastThird => Forward(index, WindowAction.LastThird, WindowAction.CenterThird, WindowAction.FirstThird),
            WindowAction.FirstTwoThirds => Forward(index, WindowAction.FirstTwoThirds, WindowAction.CenterTwoThirds, WindowAction.LastTwoThirds),
            WindowAction.CenterTwoThirds => Forward(index, WindowAction.CenterTwoThirds, WindowAction.LastTwoThirds, WindowAction.FirstTwoThirds),
            WindowAction.LastTwoThirds => Forward(index, WindowAction.LastTwoThirds, WindowAction.CenterTwoThirds, WindowAction.FirstTwoThirds),
            WindowAction.TopLeftSixth => Forward(index, WindowAction.TopLeftSixth, WindowAction.TopCenterSixth, WindowAction.TopRightSixth),
            WindowAction.TopCenterSixth => Forward(index, WindowAction.TopCenterSixth, WindowAction.TopRightSixth, WindowAction.TopLeftSixth),
            WindowAction.TopRightSixth => Forward(index, WindowAction.TopRightSixth, WindowAction.TopCenterSixth, WindowAction.TopLeftSixth),
            WindowAction.BottomLeftSixth => Forward(index, WindowAction.BottomLeftSixth, WindowAction.BottomCenterSixth, WindowAction.BottomRightSixth),
            WindowAction.BottomCenterSixth => Forward(index, WindowAction.BottomCenterSixth, WindowAction.BottomRightSixth, WindowAction.BottomLeftSixth),
            WindowAction.BottomRightSixth => Forward(index, WindowAction.BottomRightSixth, WindowAction.BottomCenterSixth, WindowAction.BottomLeftSixth),
            _ => throw new ArgumentException("A horizontally repeatable action is required.", nameof(action))
        };
    }

    private static WindowAction Forward(int index, WindowAction first, WindowAction second, WindowAction third) =>
        index switch { 0 => first, 1 => second, _ => third };
}
