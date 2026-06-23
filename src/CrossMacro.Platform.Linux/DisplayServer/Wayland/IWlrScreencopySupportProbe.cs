using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public interface IWlrScreencopySupportProbe
{
    WlrScreencopySupportResult ProbeSupport();
}

public readonly record struct WlrScreencopySupportResult
{
    private WlrScreencopySupportResult(bool isSupported, ScreenReadErrorKind? errorKind, string? errorMessage)
    {
        if (!isSupported && string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("Unavailable wlr-screencopy probes require a message.", nameof(errorMessage));
        }

        IsSupported = isSupported;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSupported { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public static WlrScreencopySupportResult Supported() => new(true, null, null);

    public static WlrScreencopySupportResult Unsupported(string errorMessage) =>
        new(false, ScreenReadErrorKind.BackendUnavailable, errorMessage);

    public static WlrScreencopySupportResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new(false, errorKind, errorMessage);
}
