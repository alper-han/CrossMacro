using System.Threading.Tasks;

namespace CrossMacro.Platform.Linux.Ipc;

internal readonly record struct PendingCaptureStartFailureContext(
    bool NotifyOnFailure,
    bool ForceReconcileOnFailure,
    CaptureCommand FailedCommand,
    PendingAsyncParticipantSnapshot[] FailedAsyncParticipants,
    CaptureCommand FailedPreviousTransportCommand,
    bool SubscriptionRemovedSinceStart,
    string[] RemovedConsumersSinceStart,
    TaskCompletionSource<bool> Completion);
