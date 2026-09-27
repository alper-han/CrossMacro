
namespace CrossMacro.Platform.Linux.Tests.Services.Factories;

public sealed class LinuxSimulatorFactoryTests
{
    [LinuxTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Create_AfterExternalPermissionChange_RevalidatesDirectSimulator(bool useLegacyAdapter)
    {
        var writable = false;
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var detector = new LinuxInputCapabilityDetector(
            _ => false, _ => writable, () => false,
            (_, _) => LinuxInputCapabilityDetector.DaemonHandshakeProbeResult.Failed(), () => now);
        var environment = Substitute.For<ILinuxEnvironmentDetector>();
        environment.IsWayland.Returns(returnThis: true);
        var snapshots = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => name is "XDG_SESSION_TYPE" ? "wayland" : null),
            detector, Substitute.For<ILinuxScreenReaderCapabilityDetector>());
        using var simulator = new LinuxInputSimulator();
        var factory = useLegacyAdapter
            ? new LinuxSimulatorFactory(environment, detector, () => simulator,
                () => throw new InvalidOperationException("No daemon"),
                () => throw new InvalidOperationException("Not X11"))
            : new LinuxSimulatorFactory(snapshots, () => simulator,
                () => throw new InvalidOperationException("No daemon"),
                () => throw new InvalidOperationException("Not X11"));

        using var initiallyUnavailable = factory.Create();
        Assert.IsType<UnavailableInputSimulator>(initiallyUnavailable);
        writable = true;
        Assert.Same(simulator, factory.Create());

        writable = false;
        using var revoked = factory.Create();
        Assert.IsType<UnavailableInputSimulator>(revoked);
    }

    [LinuxFact]
    public void Create_WhenWaylandAndDaemonMode_ReturnsIpcSimulator()
    {
        // Arrange
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Daemon);

        var legacy = new LinuxInputSimulator();
        using var ipc = new LinuxIpcInputSimulator(new IpcClient(() => "/tmp/non-existent.sock"));
        var x11FactoryCalled = false;

        var factory = new LinuxSimulatorFactory(
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
    public void Create_WhenWaylandAndLegacyMode_ReturnsLegacySimulator()
    {
        // Arrange
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Legacy);

        var legacy = new LinuxInputSimulator();
        using var ipc = new LinuxIpcInputSimulator(new IpcClient(() => "/tmp/non-existent.sock"));
        var x11FactoryCalled = false;

        var factory = new LinuxSimulatorFactory(
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
    public void Create_WhenWaylandAndNoneMode_ReturnsUnsupportedSimulator()
    {
        // Arrange
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.None);

        var legacy = new LinuxInputSimulator();
        using var ipc = new LinuxIpcInputSimulator(new IpcClient(() => "/tmp/non-existent.sock"));

        var factory = new LinuxSimulatorFactory(
            env,
            capability,
            () => legacy,
            () => ipc,
            () => throw new InvalidOperationException("X11 factory should not be used in wayland path"));

        // Act
        var result = factory.Create();

        // Assert
        Assert.False(result.IsSupported);
        _ = Assert.IsType<UnavailableInputSimulator>(result);
        Assert.Contains("No usable Linux input backend is available", ((UnavailableInputSimulator)result).FailureMessage, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void Create_WhenX11NativeSimulatorSupported_ReturnsX11BeforeCapabilityFallback()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Daemon);

        var legacy = new LinuxInputSimulator();
        using var ipc = new LinuxIpcInputSimulator(new IpcClient(() => "/tmp/non-existent.sock"));
        using var x11 = new X11InputSimulator();

        var factory = new LinuxSimulatorFactory(
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
    public void Create_WhenX11NativeSimulatorUnsupportedAndFallbackIsDaemon_ReturnsIpcSimulator()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Daemon);

        var legacy = new LinuxInputSimulator();
        using var ipc = new LinuxIpcInputSimulator(new IpcClient(() => "/tmp/non-existent.sock"));
        using var x11 = new X11InputSimulator();

        var factory = new LinuxSimulatorFactory(
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
    public void Create_WhenX11NativeSimulatorUnsupported_DisposesNativeSimulatorBeforeFallback()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Legacy);

        using var x11 = new X11InputSimulator();
        var factory = new LinuxSimulatorFactory(
            env,
            capability,
            () => new LinuxInputSimulator(),
            () => new LinuxIpcInputSimulator(new IpcClient(() => "/tmp/non-existent.sock")),
            () => x11,
            _ => false);

        _ = factory.Create();

        _ = Assert.Throws<ObjectDisposedException>(() => x11.Initialize());
    }

    [LinuxFact]
    public void Create_WhenX11NativeSimulatorUnsupportedAndFallbackIsDirect_ReturnsLegacySimulator()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.Legacy);

        var legacy = new LinuxInputSimulator();
        using var ipc = new LinuxIpcInputSimulator(new IpcClient(() => "/tmp/non-existent.sock"));
        using var x11 = new X11InputSimulator();

        var factory = new LinuxSimulatorFactory(
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
    public void Create_WhenX11NativeSimulatorUnsupportedAndFallbackIsNone_ReturnsUnsupportedSimulator()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.None);

        var legacy = new LinuxInputSimulator();
        using var ipc = new LinuxIpcInputSimulator(new IpcClient(() => "/tmp/non-existent.sock"));
        using var x11 = new X11InputSimulator();

        var factory = new LinuxSimulatorFactory(
            env,
            capability,
            () => legacy,
            () => ipc,
            () => x11,
            _ => false);

        var result = factory.Create();

        Assert.False(result.IsSupported);
        _ = Assert.IsType<UnavailableInputSimulator>(result);

        Assert.Contains("direct input fallback is unavailable", ((UnavailableInputSimulator)result).FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void Create_WhenWaylandPermissionDeniedAndNoReadableEvents_ReturnsDiagnosticReason()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        _ = env.IsWayland.Returns(returnThis: true);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        _ = capability.DetermineMode().Returns(InputProviderMode.None);
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

        var factory = new LinuxSimulatorFactory(
            env,
            capability,
            () => new LinuxInputSimulator(),
            () => throw new InvalidOperationException("IPC should not be used"),
            () => throw new InvalidOperationException("X11 should not be used"));

        var result = factory.Create();

        var unavailable = Assert.IsType<UnavailableInputSimulator>(result);
        Assert.Contains("permission denied", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("direct input fallback is unavailable", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }
}
