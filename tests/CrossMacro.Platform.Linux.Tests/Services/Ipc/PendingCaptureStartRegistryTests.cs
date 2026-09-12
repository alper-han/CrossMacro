
namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class PendingCaptureStartRegistryTests
{
    [LinuxFact]
    public void Begin_WhenStartIsAlreadyPending_ThrowsUntilCurrentRequestCompletes()
    {
        var registry = new PendingCaptureStartRegistry();
        var first = registry.Begin(new CaptureCommand(CaptureCommandType.Start), notifyOnFailure: false);

        _ = Assert.Throws<InvalidOperationException>(() =>
            registry.Begin(new CaptureCommand(CaptureCommandType.Start), notifyOnFailure: false));

        Assert.Same(first.Completion.Task, registry.TryGetPendingTaskAsync());
        Assert.True(registry.TryComplete(first.RequestId, out var completed));
        _ = completed.Completion.TrySetResult(true);
        Assert.True(completed.Completion.Task.IsCompletedSuccessfully);

        _ = registry.Begin(new CaptureCommand(CaptureCommandType.Start), notifyOnFailure: false);
    }

    [LinuxFact]
    public void TryCompleteAndTryFail_WithStaleRequestId_DoNotConsumeCurrentRequest()
    {
        var registry = new PendingCaptureStartRegistry();
        var registration = registry.Begin(new CaptureCommand(CaptureCommandType.Start), notifyOnFailure: false);

        Assert.False(registry.TryComplete(registration.RequestId + 1, out _));
        Assert.False(registry.TryFail(registration.RequestId + 1, out _));
        Assert.Same(registration.Completion.Task, registry.TryGetPendingTaskAsync());

        Assert.True(registry.TryComplete(registration.RequestId, out var completed));
        _ = completed.Completion.TrySetResult(true);
    }

    [LinuxFact]
    public async Task TryFailCurrent_WhenPendingRequestExists_ClearsRequestAndPropagatesFailure()
    {
        var registry = new PendingCaptureStartRegistry();
        var registration = registry.Begin(
            new CaptureCommand(CaptureCommandType.Start),
            notifyOnFailure: true);
        var exception = new InvalidOperationException("daemon rejected capture");

        Assert.True(registry.TryFailCurrent(exception, out var notifyOnFailure));

        Assert.True(notifyOnFailure);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => registration.Completion.Task);
        Assert.True(registry.TryGetPendingTaskAsync().IsCompletedSuccessfully);
    }

    [LinuxFact]
    public void TryReissueCurrent_UpdatesRequestAndPreservesFailureMetadata()
    {
        var registry = new PendingCaptureStartRegistry();
        var original = registry.Begin(
            new CaptureCommand(CaptureCommandType.Start, CaptureMouse: true),
            notifyOnFailure: false,
            forceReconcileOnFailure: false,
            previousTransportCommand: new CaptureCommand(CaptureCommandType.Stop),
            originConsumerId: "consumer",
            originHadPreviousSubscription: true,
            originCaptureMouse: false,
            originCaptureKeyboard: true);
        registry.MarkSubscriptionRemoved("consumer");

        Assert.True(registry.TryReissueCurrent(
            new CaptureCommand(CaptureCommandType.Start, CaptureKeyboard: true),
            notifyOnFailure: true,
            forceReconcileOnFailure: true,
            previousTransportCommand: new CaptureCommand(CaptureCommandType.Start, CaptureMouse: true),
            out var reissued));

        Assert.NotEqual(original.RequestId, reissued.RequestId);
        Assert.True(registry.TryFail(reissued.RequestId, out var failure));
        Assert.True(failure.NotifyOnFailure);
        Assert.True(failure.ForceReconcileOnFailure);
        Assert.Equal(CaptureCommandType.Start, failure.FailedCommand.Type);
        Assert.True(failure.FailedCommand.CaptureKeyboard);
        Assert.True(failure.FailedPreviousTransportCommand.CaptureMouse);
        Assert.True(failure.SubscriptionRemovedSinceStart);
        Assert.Equal(["consumer"], failure.RemovedConsumersSinceStart);
    }

    [LinuxFact]
    public void RegisterAsyncParticipant_WhenSameConsumerJoinsPendingStart_ShouldPreserveOriginRollbackSnapshot()
    {
        var registry = new PendingCaptureStartRegistry();
        var registration = registry.Begin(
            new CaptureCommand(CaptureCommandType.Start, CaptureMouse: true, CaptureKeyboard: true),
            notifyOnFailure: false,
            originConsumerId: "shared-consumer",
            originHadPreviousSubscription: true,
            originCaptureMouse: false,
            originCaptureKeyboard: true);

        registry.RegisterAsyncParticipant(
            "shared-consumer",
            hadPreviousSubscription: true,
            previousCaptureMouse: true,
            previousCaptureKeyboard: true);

        var failed = registry.TryFail(registration.RequestId, out var failureContext);

        Assert.True(failed);
        var participant = Assert.Single(failureContext.FailedAsyncParticipants);
        Assert.Equal("shared-consumer", participant.ConsumerId);
        Assert.True(participant.ShouldRestoreOnFailure);
        Assert.True(participant.HadPreviousSubscription);
        Assert.False(participant.PreviousCaptureMouse);
        Assert.True(participant.PreviousCaptureKeyboard);
    }
}
