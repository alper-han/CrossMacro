namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalStreamValidationResult
{
    private PortalStreamValidationResult(
        IReadOnlyList<PortalMonitorStream> streams,
        ScreenRect? selectedBounds,
        ScreenReadErrorKind? errorKind,
        string? errorMessage)
    {
        Streams = streams;
        SelectedBounds = selectedBounds;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public IReadOnlyList<PortalMonitorStream> Streams { get; }

    public ScreenRect? SelectedBounds { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public PortalMonitorStream Stream => Streams.Count is 1
        ? Streams[0]
        : throw new InvalidOperationException("Portal stream validation did not contain exactly one stream.");

    public static PortalStreamValidationResult Success(PortalMonitorStream stream) =>
        new([stream], stream.Bounds, errorKind: null, errorMessage: null);

    public static PortalStreamValidationResult Success(IReadOnlyList<PortalMonitorStream> streams, ScreenRect selectedBounds) =>
        new(streams, selectedBounds, errorKind: null, errorMessage: null);

    public static PortalStreamValidationResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new([], selectedBounds: null, errorKind, errorMessage);
}
