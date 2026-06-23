namespace CrossMacro.Platform.Linux.Services.ScreenReading;

internal static class LinuxScreenFrameCaptureModes
{
    public static bool SupportsRequest(LinuxScreenReaderBackend backend, bool isFullFrameRequest) =>
        !isFullFrameRequest || SupportsFullFrame(backend);

    private static bool SupportsFullFrame(LinuxScreenReaderBackend backend) => backend switch
    {
        LinuxScreenReaderBackend.KWinScreenShot2 => false,
        LinuxScreenReaderBackend.ExtImageCopy => true,
        LinuxScreenReaderBackend.WlrScreencopy => true,
        LinuxScreenReaderBackend.Portal => true,
        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unknown Linux screen reader backend.")
    };
}
