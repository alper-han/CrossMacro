
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ScreenReadingWarmupServiceTests
{
    [Fact]
    public async Task WarmUpPortalSessionAsync_WhenPortalSelected_CapturesAllSelectedStreamsOnce()
    {
        using var frameProvider = new RecordingScreenFrameProvider();
        var service = new ScreenReadingWarmupService(frameProvider, new StaticScreenReadingDiagnosticProvider("Portal"));

        await service.WarmUpPortalSessionAsync();
        await service.WarmUpPortalSessionAsync();

        Assert.Equal(1, frameProvider.CaptureCalls);
        Assert.Null(frameProvider.LastRegion);
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
            Result = ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.PermissionDenied, "denied"),
        };
        var service = new ScreenReadingWarmupService(frameProvider, new StaticScreenReadingDiagnosticProvider("Portal"));

        await service.WarmUpPortalSessionAsync();

        Assert.Equal(1, frameProvider.CaptureCalls);
    }

    [Fact]
    public async Task WarmUpPortalSessionAsync_WaitsForCapabilityReadinessBeforeReadingDiagnostics()
    {
        using var frameProvider = new RecordingScreenFrameProvider();
        var diagnostics = new MutableScreenReadingDiagnosticProvider("Portal");
        var readiness = new RecordingCapabilityReadiness(() => diagnostics.SelectedBackend = "GnomeExtension");
        var service = new ScreenReadingWarmupService(frameProvider, diagnostics, readiness);

        await service.WarmUpPortalSessionAsync();

        Assert.Equal(1, readiness.Calls);
        Assert.Equal(0, frameProvider.CaptureCalls);
    }

    private sealed class StaticScreenReadingDiagnosticProvider(string? selectedBackend) : IScreenReadingDiagnosticProvider
    {
        private readonly string? _selectedBackend = selectedBackend;

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

    private sealed class MutableScreenReadingDiagnosticProvider(string? selectedBackend) : IScreenReadingDiagnosticProvider
    {
        public string? SelectedBackend { get; set; } = selectedBackend;

        public ScreenReadingDiagnosticSnapshot GetSnapshot()
        {
            return new ScreenReadingDiagnosticSnapshot(
                IsSupportedSession: true,
                SessionKind: "Wayland",
                PolicyName: "test",
                PolicyOrder: ["Portal"],
                SelectedBackend,
                Backends: [],
                FailureBackend: null,
                FailureKind: null,
                FailureMessage: null,
                Remediation: null);
        }
    }

    private sealed class RecordingCapabilityReadiness(Action onEnsure) : IScreenReadingCapabilityReadiness
    {
        public int Calls { get; private set; }

        public Task EnsureReadyAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            onEnsure();
            return Task.CompletedTask;
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
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenFrame>(frame));
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
