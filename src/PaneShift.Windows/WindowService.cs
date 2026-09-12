using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using PaneShift.Core;

namespace PaneShift.Windows;

public sealed class WindowService
{
    private readonly WindowHistory history = new();
    private readonly RuntimeSettings runtime;
    private CommandRepetition repetition => runtime.Repetition;
    private readonly Dictionary<nint, SavedPlacement> placements = [];
    private sealed record SavedPlacement(uint ProcessId, uint ThreadId, NativeMethods.WindowPlacement Placement);

    public WindowService(RuntimeSettings? runtime = null)
    {
        this.runtime = runtime ?? new();
    }

    public WindowCommandFailure? Execute(WindowAction action) =>
        WindowCommandExecution.Execute(() => ExecuteCore(action), repetition);

    public void ResetRepetition() => repetition.Reset();

    private void ExecuteCore(WindowAction action)
    {
        var settings = runtime.Current;
        PruneHistory();
        nint hwnd = NativeMethods.GetForegroundWindow();
        if (!NativeMethods.IsWindow(hwnd) || !CanManage(hwnd)) { repetition.Reset(); return; }
        uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
        if (threadId == 0 || processId == Environment.ProcessId) { repetition.Reset(); return; }
        int repeatIndex = repetition.Next(hwnd, action,
            HalfActionCycle.AppliesTo(action) ? HalfActionCycle.Sizes.Count : 1);
        var placement = new NativeMethods.WindowPlacement { Length = Marshal.SizeOf<NativeMethods.WindowPlacement>() };
        Check(NativeMethods.GetWindowPlacement(hwnd, ref placement));
        if (action == WindowAction.Restore)
        {
            if (placements.TryGetValue(hwnd, out var saved))
            {
                Check(NativeMethods.SetWindowPlacement(hwnd, saved.Placement));
                placements.Remove(hwnd);
                history.Forget(hwnd);
            }
            return;
        }

        PixelRect work = GetWorkArea(hwnd);
        PixelRect before = GetVisibleBounds(hwnd);
        // Validate support before touching the window or its history.
        PixelRect? tile = action is WindowAction.Maximize or WindowAction.Center
            ? null : HalfActionCycle.AppliesTo(action)
                ? WindowGeometry.CalculateHalf(action, work, HalfActionCycle.Sizes[repeatIndex])
                : WindowGeometry.Calculate(action, work);
        if (tile is { } ideal)
            tile = WindowGaps.Apply(ideal, work, settings.GapPixels, settings.ApplyGapToScreenEdges);
        if (placements.TryAdd(hwnd, new(processId, threadId, placement)))
            history.Remember(hwnd, new(before, placement.ShowCommand == 3));

        if (action == WindowAction.Maximize)
        {
            placement.ShowCommand = 3; // SW_SHOWMAXIMIZED
            Check(NativeMethods.SetWindowPlacement(hwnd, placement));
            return;
        }
        if (placement.ShowCommand != 1)
        {
            placement.ShowCommand = 1; // SW_SHOWNORMAL
            placement.Flags = 0;
            Check(NativeMethods.SetWindowPlacement(hwnd, placement));
        }
        PixelRect target = tile ?? WindowGeometry.Center(GetVisibleBounds(hwnd), work);
        MoveVisibleBounds(hwnd, target);
    }

    public static PixelRect GetWorkArea(nint hwnd)
    {
        nint monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        Check(NativeMethods.GetMonitorInfo(monitor, ref info));
        return ToRect(info.Work);
    }

    private static bool CanManage(nint hwnd)
    {
        if (hwnd == 0 || hwnd == NativeMethods.GetShellWindow() || hwnd == NativeMethods.GetDesktopWindow() ||
            !NativeMethods.IsWindowVisible(hwnd)) return false;
        var name = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, name, name.Capacity);
        return name.ToString() is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW");
    }

    private static PixelRect GetVisibleBounds(nint hwnd)
    {
        if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.ExtendedFrameBounds, out var frame,
            Marshal.SizeOf<NativeMethods.Rect>()) == 0 && frame.Right > frame.Left && frame.Bottom > frame.Top)
            return ToRect(frame);
        Check(NativeMethods.GetWindowRect(hwnd, out var rect));
        return ToRect(rect);
    }

    private static void MoveVisibleBounds(nint hwnd, PixelRect target)
    {
        Check(NativeMethods.GetWindowRect(hwnd, out var outer));
        var visible = GetVisibleBounds(hwnd);
        // SetWindowPos includes invisible resize borders; our layouts describe the visible frame.
        int left = visible.X - outer.Left, top = visible.Y - outer.Top;
        int right = outer.Right - visible.Right, bottom = outer.Bottom - visible.Bottom;
        Check(NativeMethods.SetWindowPos(hwnd, 0, target.X - left, target.Y - top,
            checked(target.Width + left + right), checked(target.Height + top + bottom),
            NativeMethods.NoZOrder | NativeMethods.NoActivate));
    }

    private void PruneHistory()
    {
        foreach (var (hwnd, saved) in placements.ToArray())
        {
            uint thread = NativeMethods.GetWindowThreadProcessId(hwnd, out uint process);
            if (!NativeMethods.IsWindow(hwnd) || process != saved.ProcessId || thread != saved.ThreadId)
            {
                placements.Remove(hwnd);
                history.Forget(hwnd);
                if (repetition.Target == hwnd) repetition.Reset();
            }
        }
    }

    private static PixelRect ToRect(NativeMethods.Rect rect) =>
        new(rect.Left, rect.Top, checked(rect.Right - rect.Left), checked(rect.Bottom - rect.Top));
    private static void Check(bool success)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
