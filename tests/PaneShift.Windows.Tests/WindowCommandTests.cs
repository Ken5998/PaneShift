using System.ComponentModel;
using PaneShift.Core;

namespace PaneShift.Windows.Tests;

public class WindowCommandTests
{
    [Theory]
    [InlineData(5, WindowFailureKind.AccessDenied)]
    [InlineData(87, WindowFailureKind.NativeError)]
    [InlineData(1400, WindowFailureKind.NativeError)]
    [InlineData(0, WindowFailureKind.NativeError)]
    public void NativeErrorsUseTheCorrectPathAndResetTheCycle(int code, WindowFailureKind kind)
    {
        var repetition = new CommandRepetition();
        repetition.Next(1, WindowAction.LeftHalf, 3);
        repetition.Next(1, WindowAction.LeftHalf, 3);
        var result = WindowCommandExecution.Execute(() => throw new Win32Exception(code, "diagnostic detail"), repetition);
        Assert.NotNull(result);
        Assert.Equal(kind, result.Kind);
        Assert.Equal(code, result.NativeErrorCode);
        Assert.Contains("diagnostic detail", result.Details);
        Assert.DoesNotContain("diagnostic detail", result.Message);
        Assert.Equal(0, repetition.Next(1, WindowAction.LeftHalf, 3));
        if (code == 5) Assert.Contains("may be running with elevated privileges", result.Message);
        else Assert.DoesNotContain("elevated", result.Message);
    }

    [Fact]
    public void InvalidLayoutResetsCycleAndReturnsSafeMessage()
    {
        var repetition = new CommandRepetition();
        repetition.Next(1, WindowAction.LeftHalf, 3);
        var result = WindowCommandExecution.Execute(() => throw new ArgumentException("gap too large"), repetition);
        Assert.Equal(WindowFailureKind.InvalidOperation, result!.Kind);
        Assert.Contains("gap too large", result.Details);
        Assert.Equal(0, repetition.Next(1, WindowAction.LeftHalf, 3));
    }

    [Fact]
    public void SuccessPreservesCycle()
    {
        var repetition = new CommandRepetition();
        repetition.Next(1, WindowAction.LeftHalf, 3);
        Assert.Null(WindowCommandExecution.Execute(() => { }, repetition));
        Assert.Equal(1, repetition.Next(1, WindowAction.LeftHalf, 3));
    }

    [Fact]
    public void UnexpectedFailureResetsCycleBeforeRethrowing()
    {
        var repetition = new CommandRepetition();
        repetition.Next(1, WindowAction.LeftHalf, 3);
        Assert.Throws<InvalidOperationException>(() =>
            WindowCommandExecution.Execute(() => throw new InvalidOperationException("unexpected"), repetition));
        Assert.Equal(0, repetition.Next(1, WindowAction.LeftHalf, 3));
    }
}
