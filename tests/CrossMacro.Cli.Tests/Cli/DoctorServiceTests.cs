using CrossMacro.Core.Services;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class DoctorServiceTests
{
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly IDisplaySessionService _displaySessionService;

    public DoctorServiceTests()
    {
        _environmentInfoProvider = Substitute.For<IEnvironmentInfoProvider>();
        _displaySessionService = Substitute.For<IDisplaySessionService>();
        _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.LinuxWayland);
        _environmentInfoProvider.WindowManagerHandlesCloseButton.Returns(false);
    }

    private static IInputSimulator CreateInputSimulator(bool isSupported = true, string providerName = "test-simulator")
    {
        var simulator = Substitute.For<IInputSimulator>();
        simulator.IsSupported.Returns(isSupported);
        simulator.ProviderName.Returns(providerName);
        return simulator;
    }

    private static IInputCapture CreateInputCapture(bool isSupported = true, string providerName = "test-capture")
    {
        var capture = Substitute.For<IInputCapture>();
        capture.IsSupported.Returns(isSupported);
        capture.ProviderName.Returns(providerName);
        return capture;
    }

    private static IMousePositionProvider CreatePositionProvider(bool isSupported = true, string providerName = "test-position")
    {
        var provider = Substitute.For<IMousePositionProvider>();
        provider.IsSupported.Returns(isSupported);
        provider.ProviderName.Returns(providerName);
        return provider;
    }

    private DoctorService CreateService(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<bool>? isLinux = null,
        Func<string, bool>? daemonHandshakeProbe = null,
        Func<bool>? isWindows = null,
        Func<bool>? isMacOS = null,
        IInputSimulator? simulator = null,
        IInputCapture? capture = null,
        IMousePositionProvider? positionProvider = null,
        IPermissionChecker? permissionChecker = null)
    {
        var simulatorInstance = simulator ?? CreateInputSimulator();
        var captureInstance = capture ?? CreateInputCapture();
        var positionProviderInstance = positionProvider ?? CreatePositionProvider();

        return new DoctorService(
            _environmentInfoProvider,
            _displaySessionService,
            getEnvironmentVariable,
            fileExists,
            canOpenForWrite,
            () => simulatorInstance,
            () => captureInstance,
            positionProviderInstance,
            permissionChecker,
            isLinux,
            isWindows,
            isMacOS,
            daemonHandshakeProbe);
    }

    [Fact]
    public async Task RunAsync_WhenDisplayUnsupported_ContainsFailCheck()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = "unsupported";
            return false;
        });

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            () => true);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        Assert.True(report.HasFailures);
        Assert.Contains(report.Checks, x => x.Name == "display-session" && x.Status == DoctorCheckStatus.Fail);
    }

    [Fact]
    public async Task RunAsync_WhenDisplaySupported_ContainsPassDisplayCheck()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            () => true);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        Assert.Contains(report.Checks, x => x.Name == "display-session" && x.Status == DoctorCheckStatus.Pass);
    }

    [Fact]
    public async Task RunAsync_WhenWaylandWithWritableUInput_InputReadinessPassesWithoutDaemon()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key == "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/uinput",
            _ => true,
            () => true);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
        Assert.Contains("Daemon is not required", readiness.Message);
    }

    [Fact]
    public async Task RunAsync_WhenWaylandWithoutDaemonAndUInput_InputReadinessFails()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key == "XDG_SESSION_TYPE" ? "wayland" : null,
            _ => false,
            _ => false,
            () => true);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
    }

    [Fact]
    public async Task RunAsync_WhenDaemonSocketExistsButHandshakeFails_AndNoUInput_WaylandFails()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key == "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path == "/run/crossmacro/crossmacro.sock",
            _ => false,
            () => true,
            _ => false);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name == "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Fail, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
        Assert.Contains("handshake failed", readiness.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WhenDaemonHandshakeFailsButUInputWritable_ReportsWarnForHandshake()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key == "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/run/crossmacro/crossmacro.sock" or "/dev/uinput",
            path => path == "/dev/uinput",
            () => true,
            _ => false);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name == "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Warn, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
    }

    [Fact]
    public async Task RunAsync_WhenWindowsAndProvidersSupported_ReturnsCapabilityPassChecks()
    {
        _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.Windows);
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            isWindows: () => true,
            isMacOS: () => false);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        Assert.Contains(report.Checks, x => x.Name == "input-simulator" && x.Status == DoctorCheckStatus.Pass);
        Assert.Contains(report.Checks, x => x.Name == "input-capture" && x.Status == DoctorCheckStatus.Pass);
        Assert.Contains(report.Checks, x => x.Name == "position-provider" && x.Status == DoctorCheckStatus.Pass);
        Assert.DoesNotContain(report.Checks, x => x.Name == "macos-accessibility");
    }

    [Fact]
    public async Task RunAsync_WhenMacOSAccessibilityNotTrusted_ReturnsFailAccessibilityCheck()
    {
        _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.MacOS);
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var permissionChecker = Substitute.For<IPermissionChecker>();
        permissionChecker.IsSupported.Returns(true);
        permissionChecker.IsAccessibilityTrusted().Returns(false);

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            isWindows: () => false,
            isMacOS: () => true,
            permissionChecker: permissionChecker);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var accessibility = Assert.Single(report.Checks, x => x.Name == "macos-accessibility");
        Assert.Equal(DoctorCheckStatus.Fail, accessibility.Status);
    }

    [Fact]
    public async Task RunAsync_WhenInputSimulatorUnsupported_ReturnsFailSimulatorCheck()
    {
        _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.Windows);
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            isWindows: () => true,
            isMacOS: () => false,
            simulator: CreateInputSimulator(isSupported: false, providerName: "unsupported-sim"));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var simulatorCheck = Assert.Single(report.Checks, x => x.Name == "input-simulator");
        Assert.Equal(DoctorCheckStatus.Fail, simulatorCheck.Status);
        Assert.Contains("unavailable", simulatorCheck.Message, StringComparison.OrdinalIgnoreCase);
    }
}
