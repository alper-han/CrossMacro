
namespace CrossMacro.Platform.Linux.Ipc;

internal readonly record struct PendingCaptureStartRegistration(
    int RequestId,
    TaskCompletionSource<bool> Completion);
