namespace CrossMacro.Platform.Linux.Ipc;

internal readonly record struct PendingAsyncParticipantSnapshot(
    string ConsumerId,
    bool HadPreviousSubscription,
    bool PreviousCaptureMouse,
    bool PreviousCaptureKeyboard,
    bool ShouldRestoreOnFailure);
