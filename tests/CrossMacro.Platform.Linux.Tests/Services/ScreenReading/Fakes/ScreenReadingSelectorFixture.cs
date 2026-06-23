using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.X11;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.ScreenReading;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class ScreenReadingSelectorFixture
{
    private readonly FakeLinuxEnvironmentDetector _environmentDetector;
    private readonly FakeRuntimeContext _runtimeContext;
    private readonly FakeLinuxScreenReaderCapabilityDetector _capabilityDetector;
    private readonly FakeX11ScreenCaptureSupportProbe _x11SupportProbe;

    private ScreenReadingSelectorFixture(
        FakeLinuxEnvironmentDetector environmentDetector,
        FakeRuntimeContext runtimeContext,
        FakeLinuxScreenReaderCapabilityDetector capabilityDetector,
        FakeX11ScreenCaptureSupportProbe x11SupportProbe)
    {
        _environmentDetector = environmentDetector;
        _runtimeContext = runtimeContext;
        _capabilityDetector = capabilityDetector;
        _x11SupportProbe = x11SupportProbe;
    }

    public int ExtCreateCount { get; private set; }

    public int WlrCreateCount { get; private set; }

    public int PortalCreateCount { get; private set; }

    public int KWinCreateCount { get; private set; }

    public int X11CreateCount { get; private set; }

    public bool SnapshotRequested => _capabilityDetector.SnapshotCalls > 0;

    public int X11ProbeCalls => _x11SupportProbe.ProbeCalls;

    public LinuxScreenFrameProviderFactory CreateFactory()
    {
        return new LinuxScreenFrameProviderFactory(
            _environmentDetector,
            _runtimeContext,
            _capabilityDetector,
            _ => CreateProvider("ext", () => ExtCreateCount++),
            _ => CreateProvider("wlr", () => WlrCreateCount++),
            _ => CreateProvider("portal", () => PortalCreateCount++),
            _ => CreateProvider("kwin", () => KWinCreateCount++),
            _x11SupportProbe,
            support => CreateProvider("x11", () => X11CreateCount++, support));
    }

    public static ScreenReadingSelectorFixture Wayland(
        bool isFlatpak,
        LinuxScreenReaderBackendCapability ext,
        LinuxScreenReaderBackendCapability wlr,
        LinuxScreenReaderBackendCapability portal)
    {
        return new ScreenReadingSelectorFixture(
            new FakeLinuxEnvironmentDetector(isWayland: true, isX11: false, CompositorType.Other),
            new FakeRuntimeContext(isFlatpak),
            new FakeLinuxScreenReaderCapabilityDetector(new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.KWinScreenShot2,
                    ScreenReadErrorKind.BackendUnavailable,
                    "not kde"),
                ext,
                wlr,
                portal)),
            new FakeX11ScreenCaptureSupportProbe(X11ScreenCaptureSupportResult.Unsupported("not x11")));
    }

    public static ScreenReadingSelectorFixture NonWayland()
    {
        return new ScreenReadingSelectorFixture(
            new FakeLinuxEnvironmentDetector(isWayland: false, isX11: false, CompositorType.Unknown),
            new FakeRuntimeContext(false),
            new FakeLinuxScreenReaderCapabilityDetector(default),
            new FakeX11ScreenCaptureSupportProbe(X11ScreenCaptureSupportResult.Unsupported("not x11")));
    }

    public static ScreenReadingSelectorFixture X11(X11ScreenCaptureSupportResult support)
    {
        return new ScreenReadingSelectorFixture(
            new FakeLinuxEnvironmentDetector(isWayland: false, isX11: true, CompositorType.X11),
            new FakeRuntimeContext(false),
            new FakeLinuxScreenReaderCapabilityDetector(default),
            new FakeX11ScreenCaptureSupportProbe(support));
    }

    private static NamedScreenFrameProvider CreateProvider(
        string providerName,
        Action countCreation,
        X11ScreenCaptureSupportResult? support = null)
    {
        countCreation();
        return new NamedScreenFrameProvider(providerName, support);
    }

    private sealed class FakeLinuxEnvironmentDetector : ILinuxEnvironmentDetector
    {
        public FakeLinuxEnvironmentDetector(bool isWayland, bool isX11, CompositorType compositor)
        {
            IsWayland = isWayland;
            IsX11 = isX11;
            DetectedCompositor = compositor;
        }

        public CompositorType DetectedCompositor { get; }
        public bool IsWayland { get; }
        public bool IsX11 { get; }
    }

    private sealed class FakeX11ScreenCaptureSupportProbe : IX11ScreenCaptureSupportProbe
    {
        private readonly X11ScreenCaptureSupportResult _support;

        public FakeX11ScreenCaptureSupportProbe(X11ScreenCaptureSupportResult support)
        {
            _support = support;
        }

        public int ProbeCalls { get; private set; }

        public X11ScreenCaptureSupportResult ProbeSupport()
        {
            ProbeCalls++;
            return _support;
        }
    }

    private sealed class FakeRuntimeContext : IRuntimeContext
    {
        public FakeRuntimeContext(bool isFlatpak)
        {
            IsFlatpak = isFlatpak;
        }

        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak { get; }
        public string? SessionType => "wayland";
    }

    private sealed class FakeLinuxScreenReaderCapabilityDetector : ILinuxScreenReaderCapabilityDetector
    {
        private readonly LinuxScreenReaderCapabilitySnapshot _snapshot;

        public FakeLinuxScreenReaderCapabilityDetector(LinuxScreenReaderCapabilitySnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public int SnapshotCalls { get; private set; }

        public LinuxScreenReaderCapabilitySnapshot GetSnapshot()
        {
            SnapshotCalls++;
            return _snapshot;
        }
    }

    private sealed class NamedScreenFrameProvider : IScreenFrameProvider
    {
        private readonly X11ScreenCaptureSupportResult? _support;

        public NamedScreenFrameProvider(string providerName, X11ScreenCaptureSupportResult? support)
        {
            ProviderName = providerName;
            _support = support;
        }

        public string ProviderName { get; }
        public bool IsSupported => _support?.IsSupported ?? true;

        public Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            if (_support is { IsSupported: false } support)
            {
                return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                    support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
                    support.ErrorMessage ?? "Test provider is unavailable."));
            }

            return Task.FromResult(ScreenReadResult<ScreenFrame>.Failure(
                ScreenReadErrorKind.CaptureFailed,
                "Test provider does not capture frames."));
        }

        public void Dispose()
        {
        }
    }
}
