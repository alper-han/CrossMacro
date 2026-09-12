
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxCapabilitySnapshotProviderTests
{
    [Fact]
    public void InvalidateScreenReadingCache_DoesNotReprobeInputDaemon()
    {
        var daemonProbeCount = 0;
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["WAYLAND_DISPLAY"] = "wayland-0",
            ["DISPLAY"] = null,
        };
        var screenDetector = new LinuxScreenReaderCapabilityDetector(
            new FixedExtImageCopyProbe(),
            new FixedWlrProbe(),
            new FixedPortalProbe(),
            new FixedKWinProbe());
        var inputDetector = new LinuxInputCapabilityDetector(
            _ => true,
            _ => false,
            _ => false,
            (_, _) =>
            {
                daemonProbeCount++;
                return LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed();
            },
            () => [],
            () => DateTime.UtcNow);
        var provider = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => environment.TryGetValue(name, out var value) ? value : null),
            inputDetector,
            screenDetector);

        _ = provider.GetSnapshot();
        provider.InvalidateScreenReadingCache();
        _ = provider.GetSnapshot();

        Assert.Equal(1, daemonProbeCount);
    }

    [Fact]
    public void InvalidateCache_ReprobesInputAndScreenCapabilities()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["WAYLAND_DISPLAY"] = "wayland-0",
            ["DISPLAY"] = null,
        };
        var extProbe = new MutableExtImageCopyProbe(ExtImageCopySupportResult.Unsupported("initial"));
        var screenDetector = new LinuxScreenReaderCapabilityDetector(
            extProbe,
            new FixedWlrProbe(),
            new FixedPortalProbe(),
            new FixedKWinProbe());
        var readableInputProbeCount = 0;
        var inputDetector = new LinuxInputCapabilityDetector(
            fileExists: static _ => false,
            canOpenForWrite: static _ => false,
            hasUsableReadableInputDevices: () =>
            {
                readableInputProbeCount++;
                return false;
            },
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(),
            utcNow: static () => DateTime.UtcNow);
        var provider = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => environment.TryGetValue(name, out var value) ? value : null),
            inputDetector,
            screenDetector);

        _ = provider.GetSnapshot();
        provider.InvalidateCache();
        _ = provider.GetSnapshot();

        Assert.Equal(2, readableInputProbeCount);
        Assert.Equal(2, extProbe.CallCount);
    }

    [Fact]
    public void InputSnapshot_WhenDaemonHandshakeSucceeds_StillProbesDirectInputForSnapshotSemantics()
    {
        var directProbeCount = 0;
        var provider = new LinuxInputCapabilitySnapshotProvider(
            fileExists: path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            canOpenForWrite: _ => false,
            hasUsableReadableInputDevices: () =>
            {
                directProbeCount++;
                return false;
            },
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Success(),
            getInputEventCandidates: () => []);

        var snapshot = provider.CaptureSnapshot(TimeSpan.FromSeconds(1));

        Assert.True(snapshot.DaemonHandshakeSucceeded);
        Assert.False(snapshot.CanUseDirectUInput);
        Assert.False(snapshot.CanReadInputEvents);
        Assert.Equal(1, directProbeCount);
    }

    [Fact]
    public void InputSnapshot_WhenDaemonHandshakeFails_ProbesDirectInputFallback()
    {
        var directProbeCount = 0;
        var provider = new LinuxInputCapabilitySnapshotProvider(
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            hasUsableReadableInputDevices: () =>
            {
                directProbeCount++;
                return false;
            },
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: () => []);

        _ = provider.CaptureSnapshot(TimeSpan.FromSeconds(1));

        Assert.Equal(1, directProbeCount);
    }

    [Fact]
    public void InputSnapshot_WhenReadableInputProbeThrows_FailsClosed()
    {
        var provider = new LinuxInputCapabilitySnapshotProvider(
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            hasUsableReadableInputDevices: static () => throw new InvalidOperationException("probe failed"),
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: static () => []);

        var snapshot = provider.CaptureSnapshot(TimeSpan.FromSeconds(1));

        Assert.False(snapshot.CanReadInputEvents);
    }

    [Fact]
    public async Task InputSnapshotAsync_WhenCanceledBeforeProbe_DoesNotInvokeInputProbe()
    {
        var inputProbe = new RecordingInputDeviceAccessProbe();
        var provider = new LinuxInputCapabilitySnapshotProvider(
            fileExists: static _ => false,
            canOpenForWrite: static _ => false,
            inputDeviceAccessProbe: inputProbe,
            daemonHandshakeProbe: (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(),
            getInputEventCandidates: static () => [],
            daemonEnabled: false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => provider.CaptureSnapshotAsync(TimeSpan.FromSeconds(1), cancellation.Token).AsTask());

        Assert.Equal(0, inputProbe.AsyncCalls);
        Assert.Equal(0, inputProbe.SyncCalls);
    }

    [Fact]
    public void InputDeviceAccessProbe_UsesInjectedSyncDelegate()
    {
        var probe = new LinuxInputDeviceAccessProbe(static () => false);

        Assert.False(probe.HasUsableReadableInputDevices());
    }

    [Fact]
    public async Task InputDeviceAccessProbeAsync_UsesInjectedDelegateAndToken()
    {
        using var cancellation = new CancellationTokenSource();
        var expectedToken = cancellation.Token;
        var observedToken = default(CancellationToken);
        var probe = new LinuxInputDeviceAccessProbe(
            hasUsableReadableInputDevices: static () => false,
            hasUsableReadableInputDevicesAsync: token =>
            {
                observedToken = token;
                return ValueTask.FromResult(true);
            });

        Assert.True(await probe.HasUsableReadableInputDevicesAsync(expectedToken));
        Assert.Equal(expectedToken, observedToken);
    }

    [Fact]
    public void InvalidateCache_WhenSessionChangesToX11_SkipsWaylandScreenProbes()
    {
        var environment = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["XDG_SESSION_TYPE"] = "wayland",
            ["WAYLAND_DISPLAY"] = "wayland-0",
            ["DISPLAY"] = null,
        };
        var extProbe = new MutableExtImageCopyProbe(ExtImageCopySupportResult.Unsupported("initial"));
        var screenDetector = new LinuxScreenReaderCapabilityDetector(
            extProbe,
            new FixedWlrProbe(),
            new FixedPortalProbe(),
            new FixedKWinProbe());
        var inputDetector = new LinuxInputCapabilityDetector(
            _ => false,
            _ => false,
            _ => false,
            (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(),
            () => [],
            () => DateTime.UtcNow);
        var provider = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => environment.TryGetValue(name, out var value) ? value : null),
            inputDetector,
            screenDetector);

        Assert.Equal(CompositorType.Other, provider.GetSnapshot().Compositor);
        Assert.Equal(1, extProbe.CallCount);
        extProbe.Result = ExtImageCopySupportResult.Supported();
        environment["XDG_SESSION_TYPE"] = "x11";
        environment["WAYLAND_DISPLAY"] = null;
        environment["DISPLAY"] = ":0";

        provider.InvalidateCache();

        var refreshed = provider.GetSnapshot();
        Assert.Equal(CompositorType.X11, refreshed.Compositor);
        Assert.False(refreshed.ScreenReading.ExtImageCopy.IsAvailable);
        Assert.Equal(ScreenReadErrorKind.Unsupported, refreshed.ScreenReading.ExtImageCopy.ErrorKind);
        Assert.Equal(1, extProbe.CallCount);
    }

    private sealed class MutableExtImageCopyProbe(ExtImageCopySupportResult result) : IExtImageCopySupportProbe
    {
        public ExtImageCopySupportResult Result { get; set; } = result;
        public int CallCount { get; private set; }

        public ExtImageCopySupportResult ProbeSupport()
        {
            CallCount++;
            return Result;
        }
    }

    private sealed class FixedExtImageCopyProbe : IExtImageCopySupportProbe
    {
        public ExtImageCopySupportResult ProbeSupport() => ExtImageCopySupportResult.Unsupported("ext-image-copy");
    }

    private sealed class FixedWlrProbe : IWlrScreencopySupportProbe
    {
        public WlrScreencopySupportResult ProbeSupport() => WlrScreencopySupportResult.Unsupported("wlr");
    }

    private sealed class FixedPortalProbe : IPortalScreenCastSupportProbe
    {
        public PortalScreenCastSupportResult ProbeSupport() => PortalScreenCastSupportResult.Unsupported("portal");
    }

    private sealed class FixedKWinProbe : IKWinScreenShotSupportProbe
    {
        public KWinScreenShotSupportResult ProbeSupport() => KWinScreenShotSupportResult.Unsupported("kwin");
    }

    private sealed class RecordingInputDeviceAccessProbe : ILinuxInputDeviceAccessProbe
    {
        public int SyncCalls { get; private set; }
        public int AsyncCalls { get; private set; }

        public bool HasUsableReadableInputDevices()
        {
            SyncCalls++;
            return true;
        }

        public ValueTask<bool> HasUsableReadableInputDevicesAsync(CancellationToken cancellationToken = default)
        {
            AsyncCalls++;
            return ValueTask.FromResult(true);
        }
    }
}
