
namespace CrossMacro.Platform.Linux.Tests.Services.Factories;

public sealed class LinuxCaptureFactoryTests
{
    [LinuxTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_AfterExternalPermissionChange_RevalidatesDirectCapture(bool useLegacyAdapter)
    {
        var writable = false;
        var readable = false;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var detector = new LinuxInputCapabilityDetector(
            _ => false, _ => writable, () => readable,
            (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(), () => now);
        var environment = Substitute.For<ILinuxEnvironmentDetector>();
        environment.IsWayland.Returns(returnThis: true);
        var snapshots = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => name is "XDG_SESSION_TYPE" ? "wayland" : null),
            detector, Substitute.For<ILinuxScreenReaderCapabilityDetector>());
        using var capture = new LinuxInputCapture();
        var factory = useLegacyAdapter
            ? new LinuxCaptureFactory(environment, detector, () => capture,
                () => throw new InvalidOperationException("No daemon"),
                () => throw new InvalidOperationException("Not X11"))
            : new LinuxCaptureFactory(snapshots, () => capture,
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
        // Arrange
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Daemon);

        var legacy = new LinuxInputCapture();
        using var ipc = new LinuxIpcInputCapture(new IpcClient(() => "/tmp/non-existent.sock"), "test-capture");
        var x11FactoryCalled = false;

        var factory = new LinuxCaptureFactory(
            env,
            capability,
            () => legacy,
            () => ipc,
            () =>
            {
                x11FactoryCalled = true;
                throw new InvalidOperationException("X11 factory should not be used in wayland path");
            });

        // Act
        var result = factory.Create();

        // Assert
        Assert.Same(ipc, result);
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
    public void Create_WhenWaylandAndLegacyMode_ReturnsLegacyCapture()
    {
        // Arrange
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Legacy);
        _ = capability.CanReadInputEvents.Returns(returnThis: true);

        var legacy = new LinuxInputCapture();
        using var ipc = new LinuxIpcInputCapture(new IpcClient(() => "/tmp/non-existent.sock"), "test-capture");
        var x11FactoryCalled = false;

        var factory = new LinuxCaptureFactory(
            env,
            capability,
            () => legacy,
            () => ipc,
            () =>
            {
                x11FactoryCalled = true;
                throw new InvalidOperationException("X11 factory should not be used in wayland path");
            });

        // Act
        var result = factory.Create();

        // Assert
        Assert.Same(legacy, result);
        Assert.False(x11FactoryCalled);
    }

    [LinuxFact]
    public void Create_WhenWaylandAndNoneMode_ReturnsUnsupportedCapture()
    {
        // Arrange
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.None);

        var legacy = new LinuxInputCapture();
        using var ipc = new LinuxIpcInputCapture(new IpcClient(() => "/tmp/non-existent.sock"), "test-capture");

        var factory = new LinuxCaptureFactory(
            env,
            capability,
            () => legacy,
            () => ipc,
            () => throw new InvalidOperationException("X11 factory should not be used in wayland path"));

        // Act
        var result = factory.Create();

        // Assert
        Assert.False(result.IsSupported);
        _ = Assert.IsType<UnavailableInputCapture>(result);
        Assert.Contains("No usable Linux input capture backend is available", ((UnavailableInputCapture)result).FailureMessage, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void Create_WhenWaylandAndLegacyModeWithoutReadableEvents_ReturnsUnsupportedCapture()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Legacy);
        _ = capability.CanReadInputEvents.Returns(returnThis: false);

        var legacyFactoryCalled = false;

        using var ipc = new LinuxIpcInputCapture(new IpcClient(() => "/tmp/non-existent.sock"), "test-capture");
        var factory = new LinuxCaptureFactory(
            env,
            capability,
            () =>
            {
                legacyFactoryCalled = true;
                return new LinuxInputCapture();
            },
            () => ipc,
            () => throw new InvalidOperationException("X11 factory should not be used in wayland path"));

        var result = factory.Create();

        Assert.False(result.IsSupported);
        _ = Assert.IsType<UnavailableInputCapture>(result);
        Assert.Contains("no readable input events", ((UnavailableInputCapture)result).FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(legacyFactoryCalled);
    }

    [LinuxFact]
    public void Create_WhenWaylandAndLegacyModeWithReadableEvents_ReturnsLegacyCapture()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Legacy);
        _ = capability.CanReadInputEvents.Returns(returnThis: true);

        var legacy = new LinuxInputCapture();
        using var ipc = new LinuxIpcInputCapture(new IpcClient(() => "/tmp/non-existent.sock"), "test-capture");

        var factory = new LinuxCaptureFactory(
            env,
            capability,
            () => legacy,
            () => ipc,
            () => throw new InvalidOperationException("X11 factory should not be used in wayland path"));

        var result = factory.Create();

        Assert.Same(legacy, result);
    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureSupported_ReturnsX11BeforeCapabilityFallback()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Daemon);

        var legacy = new LinuxInputCapture();
        using var ipc = new LinuxIpcInputCapture(new IpcClient(() => "/tmp/non-existent.sock"), "test-capture");
        var x11 = CreateX11Capture();

        var factory = new LinuxCaptureFactory(
            env,
            capability,
            () => legacy,
            () => ipc,
            () => x11,
            _ => true);

        var result = factory.Create();

        Assert.Same(x11, result);
        _ = capability.DidNotReceive().DetermineMode();
    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureUnsupportedAndFallbackIsDaemon_ReturnsIpcCapture()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Daemon);

        var legacy = new LinuxInputCapture();
        using var ipc = new LinuxIpcInputCapture(new IpcClient(() => "/tmp/non-existent.sock"), "test-capture");
        var x11 = CreateX11Capture();

        var factory = new LinuxCaptureFactory(
            env,
            capability,
            () => legacy,
            () => ipc,
            () => x11,
            _ => false);

        var result = factory.Create();

        Assert.Same(ipc, result);

    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureUnsupportedAndFallbackIsDirectWithReadableEvents_ReturnsLegacyCapture()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Legacy);
        _ = capability.CanReadInputEvents.Returns(returnThis: true);

        var legacy = new LinuxInputCapture();
        using var ipc = new LinuxIpcInputCapture(new IpcClient(() => "/tmp/non-existent.sock"), "test-capture");
        var x11 = CreateX11Capture();

        var factory = new LinuxCaptureFactory(
            env,
            capability,
            () => legacy,
            () => ipc,
            () => x11,
            _ => false);

        var result = factory.Create();

        Assert.Same(legacy, result);

    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureUnsupportedAndFallbackIsNone_ReturnsUnsupportedCapture()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.None);

        var legacy = new LinuxInputCapture();
        using var ipc = new LinuxIpcInputCapture(new IpcClient(() => "/tmp/non-existent.sock"), "test-capture");
        var x11 = CreateX11Capture();

        var factory = new LinuxCaptureFactory(
            env,
            capability,
            () => legacy,
            () => ipc,
            () => x11,
            _ => false);

        var result = factory.Create();

        Assert.False(result.IsSupported);
        _ = Assert.IsType<UnavailableInputCapture>(result);

        Assert.Contains("no readable input events", ((UnavailableInputCapture)result).FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void Create_WhenWaylandPermissionDeniedAndNoReadableEvents_ReturnsDiagnosticReason()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: true);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.None);
        _ = capability.CanReadInputEvents.Returns(returnThis: false);
        _ = capability.GetSnapshot().Returns(new LinuxInputCapabilitySnapshot(
            "/run/crossmacro/crossmacro.sock",
DaemonSocketExists: true,
DaemonHandshakeSucceeded: false,
DaemonHandshakeTimedOut: false,
CanUseDirectUInput: false,
CanReadInputEvents: false,
            LinuxDaemonHandshakeProbeResult.Failed(
                "/run/crossmacro/crossmacro.sock",
                TimeSpan.FromSeconds(5),
                LinuxDaemonHandshakeStatus.PermissionDenied,
                "permission denied")));

        var factory = new LinuxCaptureFactory(
            env,
            capability,
            () => new LinuxInputCapture(),
            () => throw new InvalidOperationException("IPC should not be used"),
            () => throw new InvalidOperationException("X11 should not be used"));

        var result = factory.Create();

        var unavailable = Assert.IsType<UnavailableInputCapture>(result);
        Assert.Contains("permission denied", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no readable input events", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static X11InputCapture CreateX11Capture()
    {
        var settings = CrossMacro.Tests.SettingsServiceSubstitute.Create();
        return new X11InputCapture(new X11AbsoluteCapture(), new X11RelativeCapture(), settings);
    }

}
