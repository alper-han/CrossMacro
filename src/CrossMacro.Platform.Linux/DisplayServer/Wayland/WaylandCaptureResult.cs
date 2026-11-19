
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Outcome of one Wayland screen capture: either a frame the caller must dispose or a classified failure.
/// </summary>
public readonly record struct WaylandCaptureResult
{
    private WaylandCaptureResult(WaylandScreenFrame? frame, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (frame is null && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Failed Wayland captures require a message.", nameof(errorMessage));
        }

        Frame = frame;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public WaylandScreenFrame? Frame { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static WaylandCaptureResult Success(WaylandScreenFrame frame) =>
        new(frame ?? throw new ArgumentNullException(nameof(frame)), errorKind: null, errorMessage: null);

    public static WaylandCaptureResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(frame: null, errorKind, errorMessage);
}
