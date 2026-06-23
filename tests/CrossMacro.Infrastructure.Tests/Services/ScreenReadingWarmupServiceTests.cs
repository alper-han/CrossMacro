using CrossMacro.Infrastructure.Services.ScreenReading;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ScreenReadingWarmupServiceTests
{
    [Fact]
    public async Task WarmUpPortalSessionAsync_WhenPortalSelected_CapturesOnePixelOnce()
    {
        using var frameProvider = new RecordingScreenFrameProvider();
        var service = new ScreenReadingWarmupService(frameProvider, new StaticScreenReadingDiagnosticProvider("Portal"));

        await service.WarmUpPortalSessionAsync();
        await service.WarmUpPortalSessionAsync();

        Assert.Equal(1, frameProvider.CaptureCalls);
        Assert.Equal(new ScreenRect(0, 0, 1, 1), frameProvider.LastRegion);
        Assert.True(frameProvider.LastFrameOwner?.Disposed);
    }

    [Fact]
    public async Task WarmUpPortalSessionAsync_WhenNonPortalBackendSelected_DoesNotCapture()
    {
        using var frameProvider = new RecordingScreenFrameProvider();
        var service = new ScreenReadingWarmupService(frameProvider, new StaticScreenReadingDiagnosticProvider("WlrScreencopy"));

        await service.WarmUpPortalSessionAsync();

        Assert.Equal(0, frameProvider.CaptureCalls);
    }

    [Fact]
    public async Task WarmUpPortalSessionAsync_WhenPortalCaptureFails_CompletesWithoutThrowing()
    {
        using var frameProvider = new RecordingScreenFrameProvider
        {
            Result = ScreenReadResult<ScreenFrame>.Failure(ScreenReadErrorKind.PermissionDenied, "denied")
        };
        var service = new ScreenReadingWarmupService(frameProvider, new StaticScreenReadingDiagnosticProvider("Portal"));

        await service.WarmUpPortalSessionAsync();

        Assert.Equal(1, frameProvider.CaptureCalls);
    }

    private sealed class StaticScreenReadingDiagnosticProvider : IScreenReadingDiagnosticProvider
    {
        private readonly string? _selectedBackend;

        public StaticScreenReadingDiagnosticProvider(string? selectedBackend)
        {
            _selectedBackend = selectedBackend;
        }

        public ScreenReadingDiagnosticSnapshot GetSnapshot()
        {
            return new ScreenReadingDiagnosticSnapshot(
                IsSupportedSession: true,
                SessionKind: "Wayland",
                PolicyName: "test",
                PolicyOrder: ["Portal"],
                SelectedBackend: _selectedBackend,
                Backends: [],
                FailureBackend: null,
                FailureKind: null,
                FailureMessage: null,
                Remediation: null);
        }
    }

    private sealed class RecordingScreenFrameProvider : IScreenFrameProvider
    {
        public string ProviderName => "recording";
        public bool IsSupported => true;
        public int CaptureCalls { get; private set; }
        public ScreenRect? LastRegion { get; private set; }
        public TrackingDisposable? LastFrameOwner { get; private set; }
        public ScreenReadResult<ScreenFrame>? Result { get; set; }

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            CaptureCalls++;
            LastRegion = region;

            if (Result is { } result)
            {
                return Task.FromResult(result);
            }

            LastFrameOwner = new TrackingDisposable();
            var frame = new ScreenFrame(
                new ScreenRect(0, 0, 1, 1),
                stride: 4,
                ScreenPixelFormat.Xrgb8888,
                new byte[] { 0, 0, 0, 0 },
                LastFrameOwner);
            return Task.FromResult(ScreenReadResult<ScreenFrame>.Success(frame));
        }

        public void Dispose()
        {
        }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
