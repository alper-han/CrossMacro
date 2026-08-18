
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct WlrScreencopyCaptureResult
{
    private WlrScreencopyCaptureResult(WlrScreencopyFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed wlr-screencopy captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public WlrScreencopyFrame? Frame { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static WlrScreencopyCaptureResult Success(WlrScreencopyFrame frame) =>
        new(frame ?? throw new ArgumentNullException(nameof(frame)), errorKind: null, errorMessage: null);

    public static WlrScreencopyCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(frame: null, errorKind, errorMessage);
}
