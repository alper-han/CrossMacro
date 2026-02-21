using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Factories;
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
}
