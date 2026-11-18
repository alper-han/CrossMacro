using static CrossMacro.Platform.Linux.Tests.Services.InputSnapshotTestFactory;

namespace CrossMacro.Platform.Linux.Tests.Services.Factories;

public sealed class LinuxSimulatorFactoryTests
{
    [Fact]
    public void Create_AfterExternalPermissionChange_RevalidatesDirectSimulator()
    {
        var writable = false;
        using var input = InputSnapshotTestFactory.Create(canOpenForWrite: _ => writable);
        var snapshots = new LinuxCapabilitySnapshotProvider(
            new LinuxEnvironmentVariables(name => name is "XDG_SESSION_TYPE" ? "wayland" : null),
            input,
            Substitute.For<ILinuxScreenReaderCapabilityDetector>());
        using var simulator = new LinuxInputSimulator();
        var factory = new LinuxSimulatorFactory(
            snapshots,
            () => simulator,
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

    [Fact]
    public void Create_WhenWaylandAndDaemonMode_ReturnsIpcSimulator()
    {
        var snapshots = Capabilities(CompositorType.KDE, () => Snapshot(InputProviderMode.Daemon, daemonHandshakeSucceeded: true));
        var legacy = new LinuxInputSimulator();
        using var ipc = CreateIpcSimulator();
        var x11FactoryCalled = false;

        var factory = new LinuxSimulatorFactory(
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

    [Fact]
    public void Create_WhenWaylandAndLegacyMode_ReturnsLegacySimulator()
    {
        var snapshots = Capabilities(CompositorType.KDE, () => Snapshot(InputProviderMode.Legacy, canUseDirectUInput: true));
        var legacy = new LinuxInputSimulator();
        using var ipc = CreateIpcSimulator();

        var factory = new LinuxSimulatorFactory(
            snapshots,
            () => legacy,
            () => ipc,
            () => throw new InvalidOperationException("X11 factory should not be used in wayland path"));

        Assert.Same(legacy, factory.Create());
    }

    [Fact]
    public void Create_WhenWaylandAndNoneMode_ReturnsUnsupportedSimulator()
    {
        var snapshots = Capabilities(CompositorType.KDE, () => Snapshot(InputProviderMode.None));
        using var ipc = CreateIpcSimulator();

        var factory = new LinuxSimulatorFactory(
            snapshots,
            () => new LinuxInputSimulator(),
            () => ipc,
            () => throw new InvalidOperationException("X11 factory should not be used in wayland path"));

        var result = factory.Create();

        Assert.False(result.IsSupported);
        var unavailable = Assert.IsType<UnavailableInputSimulator>(result);
        Assert.Contains("No usable Linux input backend is available", unavailable.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WhenX11NativeSimulatorSupported_ReturnsX11WithoutConsultingDaemonOrDirectFallback()
    {
        var snapshots = Capabilities(CompositorType.X11, () => Snapshot(InputProviderMode.Daemon, daemonHandshakeSucceeded: true));
        var legacy = new LinuxInputSimulator();
        using var ipc = CreateIpcSimulator();
        using var x11 = new X11InputSimulator();

        var factory = new LinuxSimulatorFactory(snapshots, () => legacy, () => ipc, () => x11, x11IsSupported: _ => true);

        Assert.Same(x11, factory.Create());
        snapshots.DidNotReceive().InvalidateDirectInputCache();
    }

    [Fact]
    public void Create_WhenX11NativeSimulatorUnsupportedAndFallbackIsDaemon_ReturnsIpcSimulator()
    {
        var snapshots = Capabilities(CompositorType.X11, () => Snapshot(InputProviderMode.Daemon, daemonHandshakeSucceeded: true));
        var legacy = new LinuxInputSimulator();
        using var ipc = CreateIpcSimulator();
        using var x11 = new X11InputSimulator();

        var factory = new LinuxSimulatorFactory(snapshots, () => legacy, () => ipc, () => x11, x11IsSupported: _ => false);

        Assert.Same(ipc, factory.Create());
    }

    [Fact]
    public void Create_WhenX11NativeSimulatorUnsupported_DisposesNativeSimulatorBeforeFallback()
    {
        var snapshots = Capabilities(CompositorType.X11, () => Snapshot(InputProviderMode.Legacy, canUseDirectUInput: true));
        using var x11 = new X11InputSimulator();
        var factory = new LinuxSimulatorFactory(
            snapshots,
            () => new LinuxInputSimulator(),
            () => CreateIpcSimulator(),
            () => x11,
            x11IsSupported: _ => false);

        _ = factory.Create();

        _ = Assert.Throws<ObjectDisposedException>(() => x11.Initialize());
    }

    [Fact]
    public void Create_WhenX11NativeSimulatorUnsupportedAndFallbackIsDirect_ReturnsLegacySimulator()
    {
        var snapshots = Capabilities(CompositorType.X11, () => Snapshot(InputProviderMode.Legacy, canUseDirectUInput: true));
        var legacy = new LinuxInputSimulator();
        using var ipc = CreateIpcSimulator();
        using var x11 = new X11InputSimulator();

        var factory = new LinuxSimulatorFactory(snapshots, () => legacy, () => ipc, () => x11, x11IsSupported: _ => false);

        Assert.Same(legacy, factory.Create());
    }

    [Fact]
    public void Create_WhenX11NativeSimulatorUnsupportedAndFallbackIsNone_ReturnsUnsupportedSimulator()
    {
        var snapshots = Capabilities(CompositorType.X11, () => Snapshot(InputProviderMode.None));
        var legacy = new LinuxInputSimulator();
        using var ipc = CreateIpcSimulator();
        using var x11 = new X11InputSimulator();

        var factory = new LinuxSimulatorFactory(snapshots, () => legacy, () => ipc, () => x11, x11IsSupported: _ => false);

        var result = factory.Create();

        Assert.False(result.IsSupported);
        var unavailable = Assert.IsType<UnavailableInputSimulator>(result);
        Assert.Contains("direct input fallback is unavailable", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WhenWaylandPermissionDeniedAndNoDirectFallback_ReturnsDiagnosticReason()
    {
        var handshake = LinuxDaemonHandshakeProbeResult.Failed(
            IpcProtocol.DefaultSocketPath,
            TimeSpan.FromSeconds(5),
            LinuxDaemonHandshakeStatus.PermissionDenied,
            "permission denied");
        var snapshots = Capabilities(CompositorType.KDE, () => Snapshot(InputProviderMode.None, handshake: handshake));

        var factory = new LinuxSimulatorFactory(
            snapshots,
            () => new LinuxInputSimulator(),
            () => throw new InvalidOperationException("IPC should not be used"),
            () => throw new InvalidOperationException("X11 should not be used"));

        var unavailable = Assert.IsType<UnavailableInputSimulator>(factory.Create());
        Assert.Contains("permission denied", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("direct input fallback is unavailable", unavailable.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static LinuxIpcInputSimulator CreateIpcSimulator() =>
        new(new IpcClient(() => "/tmp/non-existent.sock"));
}
