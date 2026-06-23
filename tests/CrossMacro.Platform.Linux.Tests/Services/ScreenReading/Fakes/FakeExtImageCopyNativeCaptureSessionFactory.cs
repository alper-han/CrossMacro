using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakeExtImageCopyNativeCaptureSessionFactory : IExtImageCopyNativeCaptureSessionFactory, IDisposable
{
    private readonly ExtImageCopyCaptureResult _result;

    public FakeExtImageCopyNativeCaptureSessionFactory(ExtImageCopyCaptureResult result)
    {
        _result = result;
    }

    public int CaptureCalls { get; private set; }

    public ScreenRect? LastRegion { get; private set; }

    public int DisposeCount { get; private set; }

    public TimeSpan DelayBeforeResult { get; init; }

    public async Task<ExtImageCopyCaptureResult> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
    {
        CaptureCalls++;
        LastRegion = region;
        if (DelayBeforeResult > TimeSpan.Zero)
        {
            await Task.Delay(DelayBeforeResult).ConfigureAwait(false);
        }

        return _result;
    }

    public void Dispose() => DisposeCount++;
}
