namespace PaneShift.Core;

public enum WindowAction
{
    LeftHalf, RightHalf, TopHalf, BottomHalf,
    TopLeft, TopRight, BottomLeft, BottomRight,
    FirstThird, CenterThird, LastThird,
    FirstTwoThirds, CenterTwoThirds, LastTwoThirds,
    TopLeftSixth, TopCenterSixth, TopRightSixth,
    BottomLeftSixth, BottomCenterSixth, BottomRightSixth,
    Maximize, AlmostMaximize, MaximizeHeight, Center,
    MakeSmaller, MakeLarger, Restore, NextDisplay, PreviousDisplay
}
