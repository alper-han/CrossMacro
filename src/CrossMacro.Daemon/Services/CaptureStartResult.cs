namespace CrossMacro.Daemon.Services;

internal readonly record struct CaptureStartResult(
    bool Success,
    int StartedDeviceCount,
    string? ErrorMessage = null)
{
    public static CaptureStartResult Started(int startedDeviceCount) =>
        new(Success: true, startedDeviceCount);

    public static CaptureStartResult Failed(string errorMessage) =>
        new(Success: false, 0, errorMessage);
}
