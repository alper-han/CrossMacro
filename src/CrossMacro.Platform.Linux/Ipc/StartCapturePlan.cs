namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>What a start request has to do after registering under the state lock.</summary>
/// <param name="WaitTask">The pending start to wait for; already completed when none is in flight.</param>
/// <param name="PendingStart">The registration this request created, when it issues a start command.</param>
/// <param name="Command">The command to send, or <see cref="CaptureCommandType.None"/>.</param>
/// <param name="JoinedExistingPendingStart">Whether the request attached to a start another consumer issued.</param>
/// <param name="Previous">The subscription to restore if the request fails.</param>
internal readonly record struct StartCapturePlan(
    Task WaitTask,
    PendingCaptureStartRegistration? PendingStart,
    CaptureCommand Command,
    bool JoinedExistingPendingStart,
    PreviousSubscription Previous);
