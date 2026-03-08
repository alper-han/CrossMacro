using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public class PendingCaptureStartRegistryTests
{
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
