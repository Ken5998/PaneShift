using System.Globalization;

namespace PaneShift.Windows;

/// <summary>Explicit startup handoff. The timestamp distinguishes the old instance from a reused PID.</summary>
public sealed record RestartRequest(int ParentProcessId, long ParentStartTimeUtcTicks, string SettingsFile, bool Paused)
{
    public string[] ToArguments() =>
    [
        "--restart-from", ParentProcessId.ToString(CultureInfo.InvariantCulture),
        ParentStartTimeUtcTicks.ToString(CultureInfo.InvariantCulture),
        SettingsFile, Paused ? "paused" : "active"
    ];

    public static RestartRequest? Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0) return null;
        if (arguments.Count != 5 || arguments[0] != "--restart-from" ||
            !int.TryParse(arguments[1], NumberStyles.None, CultureInfo.InvariantCulture, out int processId) || processId <= 0 ||
            !long.TryParse(arguments[2], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) ||
            ticks <= 0 || ticks > DateTime.MaxValue.Ticks ||
            !Path.IsPathFullyQualified(arguments[3]) ||
            arguments[4] is not ("paused" or "active"))
            throw new ArgumentException("Invalid PaneShift restart arguments.");
        return new(processId, ticks, Path.GetFullPath(arguments[3]), arguments[4] == "paused");
    }
}
