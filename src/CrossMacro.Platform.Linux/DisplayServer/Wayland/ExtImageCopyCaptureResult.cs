
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public readonly record struct ExtImageCopyCaptureResult
{
    private ExtImageCopyCaptureResult(ExtImageCopyFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed ext-image-copy captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public ExtImageCopyFrame? Frame { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static ExtImageCopyCaptureResult Success(ExtImageCopyFrame frame) =>
        new(frame ?? throw new ArgumentNullException(nameof(frame)), errorKind: null, errorMessage: null);

    public static ExtImageCopyCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(frame: null, errorKind, errorMessage);
}
