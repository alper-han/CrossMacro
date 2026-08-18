
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class UnsupportedScreenFrameProvider : IScreenFrameProvider
{
    public string ProviderName => "Unsupported screen frame provider";

    public bool IsSupported => false;

    public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        _ = region;
        _ = options;

        return Task.FromResult(ScreenReadResultFactory.Failure<ScreenFrame>(
            ScreenReadErrorKind.Unsupported,
            "Screen reading is not supported by the active platform registrar."));
    }

    public void Dispose()
    {
    }
}
