
namespace CrossMacro.Platform.Linux.DisplayServer.X11;

public readonly record struct X11ScreenCaptureResult
{
    private X11ScreenCaptureResult(X11ScreenCaptureFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed X11 captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public X11ScreenCaptureFrame? Frame { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static X11ScreenCaptureResult Success(X11ScreenCaptureFrame frame) =>
        new(frame ?? throw new ArgumentNullException(nameof(frame)), errorKind: null, errorMessage: null);

    public static X11ScreenCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(frame: null, errorKind, errorMessage);
}
