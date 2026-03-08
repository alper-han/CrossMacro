using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.TestInfrastructure;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public class CaptureStartFailureReconcilerTests
{
    [LinuxFact]
    public void ShouldReconcile_WhenRollbackChangedSubscriptionsAndMaskIsSame_ReturnsTrue()
    {
        var currentRequiredCommand = new CaptureCommand(
            CaptureCommandType.Start,
            CaptureMouse: true,
            CaptureKeyboard: true);
        var failedCommand = new CaptureCommand(
            CaptureCommandType.Start,
            CaptureMouse: true,
            CaptureKeyboard: true);

        var shouldReconcile = CaptureStartFailureReconciler.ShouldReconcile(
            currentRequiredCommand,
            failedCommand,
            allowSameCommandRetry: false,
            subscriptionRemovedSinceStart: false,
            rollbackChangedSubscriptions: true);

        Assert.True(shouldReconcile);
    }

    [LinuxFact]
    public void ShouldReconcile_WhenMaskIsSameAndNoStateChange_ReturnsFalse()
    {
        var currentRequiredCommand = new CaptureCommand(
            CaptureCommandType.Start,
            CaptureMouse: true,
            CaptureKeyboard: true);
        var failedCommand = new CaptureCommand(
            CaptureCommandType.Start,
            CaptureMouse: true,
            CaptureKeyboard: true);

        var shouldReconcile = CaptureStartFailureReconciler.ShouldReconcile(
            currentRequiredCommand,
            failedCommand,
            allowSameCommandRetry: false,
            subscriptionRemovedSinceStart: false,
            rollbackChangedSubscriptions: false);

        Assert.False(shouldReconcile);
    }

    [LinuxFact]
    public void ShouldReconcile_WhenCurrentRequiredCommandIsNotStart_ReturnsFalse()
    {
        var shouldReconcile = CaptureStartFailureReconciler.ShouldReconcile(
            currentRequiredCommand: default,
            failedCommand: new CaptureCommand(CaptureCommandType.Start, CaptureMouse: true, CaptureKeyboard: false),
            allowSameCommandRetry: true,
            subscriptionRemovedSinceStart: true,
            rollbackChangedSubscriptions: true);

        Assert.False(shouldReconcile);
    }
}
