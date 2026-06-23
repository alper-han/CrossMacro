using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class UnavailableLinuxScreenFrameProvider : IScreenFrameProvider
{
    public UnavailableLinuxScreenFrameProvider(ScreenReadErrorKind errorKind, string failureMessage)
    {
        if (string.IsNullOrWhiteSpace(failureMessage))
        {
            throw new ArgumentException("Unavailable screen frame providers require a message.", nameof(failureMessage));
        }

        ErrorKind = errorKind;
        FailureMessage = failureMessage;
    }

    public string ProviderName => "Linux Screen Reader (Unavailable)";

    public bool IsSupported => false;

    public ScreenReadErrorKind ErrorKind { get; }

    public string FailureMessage { get; }

    public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(ErrorKind, FailureMessage));
    }

    public void Dispose()
    {
    }
}
