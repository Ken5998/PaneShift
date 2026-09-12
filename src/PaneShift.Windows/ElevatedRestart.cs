using System.ComponentModel;
using System.Diagnostics;

namespace PaneShift.Windows;

public enum RestartOutcome { Started, Cancelled, Failed }
public sealed record RestartResult(RestartOutcome Outcome, string? Details = null);

public static class ElevatedRestart
{
    public static ProcessStartInfo CreateStartInfo(string executable, RestartRequest request)
    {
        if (!Path.IsPathFullyQualified(executable)) throw new ArgumentException("An absolute executable path is required.", nameof(executable));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(executable)!
        };
        foreach (string argument in request.ToArguments()) start.ArgumentList.Add(argument);
        return start;
    }

    public static RestartResult Start(string executable, string settingsFile, bool paused)
    {
        try
        {
            if (!File.Exists(executable)) return new(RestartOutcome.Failed, $"Executable not found: {executable}");
            using var current = Process.GetCurrentProcess();
            var request = new RestartRequest(current.Id, current.StartTime.ToUniversalTime().Ticks, settingsFile, paused);
            using var next = Process.Start(CreateStartInfo(executable, request));
            return next is null ? new(RestartOutcome.Failed, "Windows did not return the new process.") : new(RestartOutcome.Started);
        }
        catch (Win32Exception ex) { return FromLaunchError(ex.NativeErrorCode, ex.Message); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new(RestartOutcome.Failed, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public static RestartResult FromLaunchError(int code, string detail) =>
        new(code == 1223 ? RestartOutcome.Cancelled : RestartOutcome.Failed, $"Win32 error {code}: {detail}");

    /// <summary>Called before mutex acquisition. This is a bounded OS process wait, not polling.</summary>
    public static void WaitForPreviousInstance(RestartRequest request)
    {
        if (request.ParentProcessId == Environment.ProcessId)
            throw new ArgumentException("A process cannot wait for itself during restart.");
        Process previous;
        try { previous = Process.GetProcessById(request.ParentProcessId); }
        catch (ArgumentException) { return; } // It already exited before the new process reached startup.
        using (previous)
        {
            try
            {
                if (previous.HasExited || previous.StartTime.ToUniversalTime().Ticks != request.ParentStartTimeUtcTicks) return;
            }
            catch (InvalidOperationException) { return; } // Exit raced with the metadata read.
            if (!previous.WaitForExit(30_000))
                throw new TimeoutException("The previous PaneShift instance did not exit. The new instance will close without registering shortcuts.");
        }
    }
}
