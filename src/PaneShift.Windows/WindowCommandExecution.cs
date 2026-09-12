using System.ComponentModel;
using PaneShift.Core;

namespace PaneShift.Windows;

public enum WindowFailureKind { AccessDenied, NativeError, InvalidOperation }

public sealed record WindowCommandFailure(WindowFailureKind Kind, string Message, string Details, int? NativeErrorCode = null);

/// <summary>Translates command failures at the native boundary and resets consecutive-command state.</summary>
public static class WindowCommandExecution
{
    public static WindowCommandFailure? Execute(Action command, CommandRepetition repetition)
    {
        try { command(); return null; }
        catch (Win32Exception ex)
        {
            repetition.Reset();
            return FromNativeError(ex.NativeErrorCode, ex.Message);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or OverflowException)
        {
            repetition.Reset();
            return new(WindowFailureKind.InvalidOperation,
                "PaneShift could not position this window. See Shortcuts and status for details.",
                $"{ex.GetType().Name}: {ex.Message}");
        }
        catch
        {
            repetition.Reset();
            throw;
        }
    }

    public static WindowCommandFailure FromNativeError(int code, string detail) => code == 5
        ? new(WindowFailureKind.AccessDenied,
            "Windows denied access to this window. It may be running with elevated privileges. " +
            "To control elevated windows, choose Restart as administrator from the tray.",
            $"Win32 error {code}: {detail}", code)
        : new(WindowFailureKind.NativeError,
            "PaneShift could not control this window. See Shortcuts and status for details.",
            $"Win32 error {code}: {detail}", code);
}
