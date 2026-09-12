namespace PaneShift.Core;

public enum HalfSize { Half, TwoThirds, Third }

public static class HalfActionCycle
{
    public static IReadOnlyList<HalfSize> Sizes { get; } =
        Array.AsReadOnly(new[] { HalfSize.Half, HalfSize.TwoThirds, HalfSize.Third });

    public static bool AppliesTo(WindowAction action) => action is
        WindowAction.LeftHalf or WindowAction.RightHalf or WindowAction.TopHalf or WindowAction.BottomHalf;
}
