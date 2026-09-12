namespace PaneShift.Core;

public sealed record ActionDefinition(WindowAction Action, string Name, string Group)
{
    public string Id => char.ToLowerInvariant(Action.ToString()[0]) + Action.ToString()[1..];
}

public static class ActionCatalog
{
    public static IReadOnlyList<ActionDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new ActionDefinition(WindowAction.LeftHalf, "Left Half", "Halves"),
        new(WindowAction.RightHalf, "Right Half", "Halves"), new(WindowAction.TopHalf, "Top Half", "Halves"), new(WindowAction.BottomHalf, "Bottom Half", "Halves"),
        new(WindowAction.TopLeft, "Top Left", "Corners"), new(WindowAction.TopRight, "Top Right", "Corners"),
        new(WindowAction.BottomLeft, "Bottom Left", "Corners"), new(WindowAction.BottomRight, "Bottom Right", "Corners"),
        new(WindowAction.FirstThird, "First Third", "Thirds"), new(WindowAction.CenterThird, "Center Third", "Thirds"), new(WindowAction.LastThird, "Last Third", "Thirds"),
        new(WindowAction.FirstTwoThirds, "First Two Thirds", "Two Thirds"), new(WindowAction.CenterTwoThirds, "Center Two Thirds", "Two Thirds"), new(WindowAction.LastTwoThirds, "Last Two Thirds", "Two Thirds"),
        new(WindowAction.TopLeftSixth, "Top Left Sixth", "Sixths"), new(WindowAction.TopCenterSixth, "Top Center Sixth", "Sixths"), new(WindowAction.TopRightSixth, "Top Right Sixth", "Sixths"),
        new(WindowAction.BottomLeftSixth, "Bottom Left Sixth", "Sixths"), new(WindowAction.BottomCenterSixth, "Bottom Center Sixth", "Sixths"), new(WindowAction.BottomRightSixth, "Bottom Right Sixth", "Sixths"),
        new(WindowAction.Maximize, "Maximize", "Other"), new(WindowAction.Center, "Center", "Other"), new(WindowAction.Restore, "Restore", "Other")
    });

    public static ActionDefinition Get(WindowAction action) => All.First(a => a.Action == action);
}
