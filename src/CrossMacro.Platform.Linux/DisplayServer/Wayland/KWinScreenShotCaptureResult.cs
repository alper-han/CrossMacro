using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct KWinScreenShotCaptureResult
{
    private KWinScreenShotCaptureResult(KWinScreenShotFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed KWin screenshot captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;
    public KWinScreenShotFrame? Frame { get; }
    public ScreenReadErrorKind? ErrorKind { get; }
    public string? ErrorMessage { get; }

    public static KWinScreenShotCaptureResult Success(KWinScreenShotFrame frame) => new(frame ?? throw new ArgumentNullException(nameof(frame)), errorKind: null, errorMessage: null);
    public static KWinScreenShotCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) => new(frame: null, errorKind, errorMessage);
}
