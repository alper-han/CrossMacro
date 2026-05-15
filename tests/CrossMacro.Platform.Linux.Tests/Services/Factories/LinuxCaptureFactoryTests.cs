using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Factories;
using CrossMacro.Platform.Abstractions.Diagnostics;
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
        capability.CanReadInputEvents.Returns(true);

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
        env.IsWayland.Returns(true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.None);

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
        Assert.IsType<UnavailableInputCapture>(result);
        Assert.Contains("No usable Linux input capture backend is available", ((UnavailableInputCapture)result).FailureMessage);
    }

    [LinuxFact]
    public void Create_WhenWaylandAndLegacyModeWithoutReadableEvents_ReturnsUnsupportedCapture()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Legacy);
        capability.CanReadInputEvents.Returns(false);

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
        Assert.IsType<UnavailableInputCapture>(result);
        Assert.Contains("no readable input events", ((UnavailableInputCapture)result).FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(legacyFactoryCalled);
    }

    [LinuxFact]
    public void Create_WhenWaylandAndLegacyModeWithReadableEvents_ReturnsLegacyCapture()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(true);
        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Legacy);
        capability.CanReadInputEvents.Returns(true);

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
        env.IsWayland.Returns(false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Daemon);

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
        capability.DidNotReceive().DetermineMode();
    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureUnsupportedAndFallbackIsDaemon_ReturnsIpcCapture()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Daemon);

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
        capability.Received(1).DetermineMode();
    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureUnsupportedAndFallbackIsDirectWithReadableEvents_ReturnsLegacyCapture()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.Legacy);
        capability.CanReadInputEvents.Returns(true);

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
        capability.Received(1).DetermineMode();
    }

    [LinuxFact]
    public void Create_WhenX11NativeCaptureUnsupportedAndFallbackIsNone_ReturnsUnsupportedCapture()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(false);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.None);

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
        Assert.IsType<UnavailableInputCapture>(result);
        capability.Received(1).DetermineMode();
        Assert.Contains("no readable input events", ((UnavailableInputCapture)result).FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void Create_WhenWaylandPermissionDeniedAndNoReadableEvents_ReturnsDiagnosticReason()
    {
        var env = Substitute.For<ILinuxEnvironmentDetector>();
        env.IsWayland.Returns(true);

        var capability = Substitute.For<ILinuxInputCapabilityDetector>();
        capability.DetermineMode().Returns(InputProviderMode.None);
        capability.CanReadInputEvents.Returns(false);
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
        var settings = Substitute.For<ISettingsService>();
        return new X11InputCapture(new X11AbsoluteCapture(), new X11RelativeCapture(), settings);
    }

}
