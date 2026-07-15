using CrossMacro.Platform.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct PortalScreenCastCaptureResult
{
    private PortalScreenCastCaptureResult(PortalPipeWireFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed portal captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;
    public PortalPipeWireFrame? Frame { get; }
    public ScreenReadErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }
    public static PortalScreenCastCaptureResult Success(PortalPipeWireFrame frame) => new(frame ?? throw new ArgumentNullException(nameof(frame)), errorKind: null, errorMessage: null);
    public static PortalScreenCastCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(frame: null, errorKind, errorMessage);
}
