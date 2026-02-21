using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Factories;
using CrossMacro.TestInfrastructure;
using NSubstitute;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services.Factories;

public class LinuxCaptureFactoryTests
{
    [LinuxFact]
    public void Create_WhenWaylandAndDaemonMode_ReturnsIpcCapture()
    {
        // Arrange
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Daemon);

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
    public void Create_WhenWaylandAndLegacyMode_ReturnsLegacyCapture()
    {
        // Arrange
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Legacy);

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
}
