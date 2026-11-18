using static CrossMacro.Platform.Linux.Tests.Services.InputSnapshotTestFactory;

namespace CrossMacro.Platform.Linux.Tests.Services.Factories;

public sealed class LinuxCaptureFactoryTests
{
    [LinuxFact]
    public void Create_AfterExternalPermissionChange_RevalidatesDirectCapture()
    {
        var writable = false;
        var readable = false;
        using var input = InputSnapshotTestFactory.Create(canOpenForWrite: _ => writable, hasReadableInputDevices: () => readable);
        var snapshots = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => name is "XDG_SESSION_TYPE" ? "wayland" : null),
            input,
            Substitute.For<ILinuxScreenReaderCapabilityDetector>());
        using var capture = new LinuxInputCapture();
        var factory = new LinuxCaptureFactory(
            snapshots,
            () => capture,
            () => throw new InvalidOperationException("No daemon"),
            () => throw new InvalidOperationException("Not X11"));

        using var initiallyUnavailable = factory.Create();
        Assert.IsType<UnavailableInputCapture>(initiallyUnavailable);
        writable = true;
        readable = true;
        Assert.Same(capture, factory.Create());

        readable = false;
        using var revoked = factory.Create();
        Assert.IsType<UnavailableInputCapture>(revoked);

        readable = true;
        writable = false;
        using var missingUInput = factory.Create();
        Assert.IsType<UnavailableInputCapture>(missingUInput);
    }

    [LinuxFact]
    public void Create_WhenWaylandAndDaemonMode_ReturnsIpcCapture()
    {
        var snapshots = Capabilities(CompositorType.KDE, () => Snapshot(InputProviderMode.Daemon, daemonHandshakeSucceeded: true));
        var legacy = new LinuxInputCapture();
        using var ipc = CreateIpcCapture();
        var x11FactoryCalled = false;

        var factory = new LinuxCaptureFactory(
            snapshots,
            () => legacy,
            () => ipc,
            () =>
            {
                x11FactoryCalled = true;
                throw new InvalidOperationException("X11 factory should not be used in wayland path");
            });

        Assert.Same(ipc, factory.Create());
        Assert.False(x11FactoryCalled);
    }

    [LinuxFact]
    public async Task UnavailableCapture_WhenStartIsCanceled_DoesNotReportUnsupportedBackendError()
    {
        using var capture = new UnavailableInputCapture();
        var errorCount = 0;
        capture.CaptureError += (_, _) => errorCount++;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => capture.StartAsync(cancellation.Token));

        Assert.Equal(0, errorCount);
    }

    [LinuxFact]
    public void Create_WhenWaylandAndLegacyModeWithReadableEvents_ReturnsLegacyCapture()
    {
        var snapshots = Capabilities(CompositorType.KDE, () => Snapshot(InputProviderMode.Legacy, canUseDirectUInput: true, canReadInputEvents: true));
        var legacy = new LinuxInputCapture();
        using var ipc = CreateIpcCapture();

        var factory = new LinuxCaptureFactory(
            snapshots,
            () => legacy,
            () => ipc,
            () => throw new InvalidOperationException("X11 factory should not be used in wayland path"));

        Assert.Same(legacy, factory.Create());
    }

    [LinuxFact]
    public void Create_WhenWaylandAndNoneMode_ReturnsUnsupportedCapture()
    {
        var snapshots = Capabilities(CompositorType.KDE, () => Snapshot(InputProviderMode.None));
        using var ipc = CreateIpcCapture();

        var factory = new LinuxCaptureFactory(
            snapshots,
            () => new LinuxInputCapture(),
            () => ipc,
            () => throw new InvalidOperationException("X11 factory should not be used in wayland path"));

        var result = factory.Create();

        Assert.False(result.IsSupported);
        var unavailable = Assert.IsType<UnavailableInputCapture>(result);
        Assert.Contains("No usable Linux input capture backend is available", unavailable.FailureMessage, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void Create_WhenWaylandAndLegacyModeWithoutReadableEvents_ReturnsUnsupportedCapture()
    {
        var snapshots = Capabilities(CompositorType.KDE, () => Snapshot(InputProviderMode.Legacy, canUseDirectUInput: true, canReadInputEvents: false));
        var legacyFactoryCalled = false;
        using var ipc = CreateIpcCapture();

        var factory = new LinuxCaptureFactory(
            snapshots,
            () =>
            {
                legacyFactoryCalled = true;
                return new LinuxInputCapture();
            },
            () => ipc,
            () => throw new InvalidOperationException("X11 factory should not be used in wayland path"));

        var result = factory.Create();

        Assert.False(result.IsSupported);
        var unavailable = Assert.IsType<UnavailableInputCapture>(result);
        Assert.Contains("no readable input events", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(legacyFactoryCalled);
    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureSupported_ReturnsX11WithoutConsultingDaemonOrDirectFallback()
    {
        var snapshots = Capabilities(CompositorType.X11, () => Snapshot(InputProviderMode.Daemon, daemonHandshakeSucceeded: true));
        var legacy = new LinuxInputCapture();
        using var ipc = CreateIpcCapture();
        var x11 = CreateX11Capture();

        var factory = new LinuxCaptureFactory(snapshots, () => legacy, () => ipc, () => x11, _ => true);

        Assert.Same(x11, factory.Create());
        snapshots.DidNotReceive().InvalidateDirectInputCache();
    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureUnsupportedAndFallbackIsDaemon_ReturnsIpcCapture()
    {
        var snapshots = Capabilities(CompositorType.X11, () => Snapshot(InputProviderMode.Daemon, daemonHandshakeSucceeded: true));
        var legacy = new LinuxInputCapture();
        using var ipc = CreateIpcCapture();
        var x11 = CreateX11Capture();

        var factory = new LinuxCaptureFactory(snapshots, () => legacy, () => ipc, () => x11, _ => false);

        Assert.Same(ipc, factory.Create());
    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureUnsupportedAndFallbackIsDirectWithReadableEvents_ReturnsLegacyCapture()
    {
        var snapshots = Capabilities(CompositorType.X11, () => Snapshot(InputProviderMode.Legacy, canUseDirectUInput: true, canReadInputEvents: true));
        var legacy = new LinuxInputCapture();
        using var ipc = CreateIpcCapture();
        var x11 = CreateX11Capture();

        var factory = new LinuxCaptureFactory(snapshots, () => legacy, () => ipc, () => x11, _ => false);

        Assert.Same(legacy, factory.Create());
    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureUnsupportedAndFallbackIsNone_ReturnsUnsupportedCapture()
    {
        var snapshots = Capabilities(CompositorType.X11, () => Snapshot(InputProviderMode.None));
        var legacy = new LinuxInputCapture();
        using var ipc = CreateIpcCapture();
        var x11 = CreateX11Capture();

        var factory = new LinuxCaptureFactory(snapshots, () => legacy, () => ipc, () => x11, _ => false);

        var result = factory.Create();

        Assert.False(result.IsSupported);
        var unavailable = Assert.IsType<UnavailableInputCapture>(result);
        Assert.Contains("no readable input events", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureUnsupported_DisposesTheNativeCaptureBeforeFallback()
    {
        var snapshots = Capabilities(CompositorType.X11, () => Snapshot(InputProviderMode.None));
        var x11 = CreateX11Capture();
        var factory = new LinuxCaptureFactory(
            snapshots,
            () => new LinuxInputCapture(),
            () => throw new InvalidOperationException("IPC should not be used"),
            () => x11,
            _ => false);

        using var result = factory.Create();

        Assert.IsType<UnavailableInputCapture>(result);
        _ = Assert.Throws<ObjectDisposedException>(() => x11.StartAsync(CancellationToken.None).GetAwaiter().GetResult());
    }

    [LinuxFact]
    public void Create_WhenNoDaemonIsAvailable_RevalidatesDirectAccessBeforeChoosingBackend()
    {
        var snapshots = Capabilities(CompositorType.KDE, () => Snapshot(InputProviderMode.None));

        _ = new LinuxCaptureFactory(
                snapshots,
                () => new LinuxInputCapture(),
                () => throw new InvalidOperationException("IPC should not be used"),
                () => throw new InvalidOperationException("X11 should not be used"))
            .Create();

        snapshots.Received(1).InvalidateDirectInputCache();
    }

    [LinuxFact]
    public void Create_WhenWaylandPermissionDeniedAndNoReadableEvents_ReturnsDiagnosticReason()
    {
        var handshake = LinuxDaemonHandshakeProbeResult.Failed(
            IpcProtocol.DefaultSocketPath,
            TimeSpan.FromSeconds(5),
            LinuxDaemonHandshakeStatus.PermissionDenied,
            "permission denied");
        var snapshots = Capabilities(CompositorType.KDE, () => Snapshot(InputProviderMode.None, handshake: handshake));

        var factory = new LinuxCaptureFactory(
            snapshots,
            () => new LinuxInputCapture(),
            () => throw new InvalidOperationException("IPC should not be used"),
            () => throw new InvalidOperationException("X11 should not be used"));

        var unavailable = Assert.IsType<UnavailableInputCapture>(factory.Create());
        Assert.Contains("permission denied", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no readable input events", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static LinuxIpcInputCapture CreateIpcCapture() =>
        new(new IpcClient(() => "/tmp/non-existent.sock"), "test-capture");

    private static X11InputCapture CreateX11Capture()
    {
        var settings = SettingsServiceSubstitute.Create();
        return new X11InputCapture(new X11AbsoluteCapture(), new X11RelativeCapture(), settings);
    }
}
