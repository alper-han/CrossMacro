
namespace CrossMacro.Platform.Abstractions;

public interface IScreenFrameProvider : IDisposable
{
    public string ProviderName { get; }

    public bool IsSupported { get; }

    public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options);
}
