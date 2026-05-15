using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Factories;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.TestInfrastructure;
using NSubstitute;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services.Factories;

public class LinuxSimulatorFactoryTests
{
    [LinuxFact]
    public void Create_WhenWaylandAndDaemonMode_ReturnsIpcSimulator()
    {
        // Arrange
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Daemon);

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
        env.IsWayland.Returns(true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Legacy);

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
        env.IsWayland.Returns(true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.None);

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
        Assert.IsType<UnavailableInputSimulator>(result);
        Assert.Contains("No usable Linux input backend is available", ((UnavailableInputSimulator)result).FailureMessage);
    }

    [LinuxFact]
    public void Create_WhenX11NativeSimulatorSupported_ReturnsX11BeforeCapabilityFallback()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Daemon);

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
        capability.DidNotReceive().DetermineMode();
    }

    [LinuxFact]
    public void Create_WhenX11NativeSimulatorUnsupportedAndFallbackIsDaemon_ReturnsIpcSimulator()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Daemon);

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
        capability.Received(1).DetermineMode();
    }

    [LinuxFact]
    public void Create_WhenX11NativeSimulatorUnsupportedAndFallbackIsDirect_ReturnsLegacySimulator()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Legacy);

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
        capability.Received(1).DetermineMode();
    }

    [LinuxFact]
    public void Create_WhenX11NativeSimulatorUnsupportedAndFallbackIsNone_ReturnsUnsupportedSimulator()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.None);

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
        Assert.IsType<UnavailableInputSimulator>(result);
        capability.Received(1).DetermineMode();
        Assert.Contains("direct input fallback is unavailable", ((UnavailableInputSimulator)result).FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void Create_WhenWaylandPermissionDeniedAndNoReadableEvents_ReturnsDiagnosticReason()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(true);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.None);
        capability.GetSnapshot().Returns(new LinuxInputCapabilitySnapshot(
            "/run/crossmacro/crossmacro.sock",
            true,
            false,
            false,
            false,
            false,
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
