using CrossMacro.Core.Services;
using CrossMacro.Daemon.Contracts.Ipc;
using CrossMacro.Cli.Services;
using CrossMacro.Platform.Abstractions.Diagnostics;
using FluentAssertions;
using NSubstitute;
using System.Reflection;

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
        Func<string, LinuxDaemonSocketAccessResult>? daemonSocketAccessProbe = null,
        Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult>? daemonHandshakeDiagnosticProbe = null,
        Func<bool>? isWindows = null,
        Func<bool>? isMacOS = null,
        IInputSimulator? simulator = null,
        IInputCapture? capture = null,
        IMousePositionProvider? positionProvider = null,
        IPermissionChecker? permissionChecker = null,
        Func<string, string?>? readAllTextIfExists = null,
        Func<string, bool>? canOpenForRead = null,
        Func<string[]>? getInputEventCandidates = null)
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
            canOpenForRead,
            getInputEventCandidates,
            () => simulatorInstance,
            () => captureInstance,
            positionProviderInstance,
            permissionChecker,
            isLinux,
            isWindows,
            isMacOS,
            daemonHandshakeProbe,
            daemonSocketAccessProbe,
            daemonHandshakeDiagnosticProbe,
            readAllTextIfExists ?? (_ => null));
    }

    private static string? GetDetailsString(DoctorCheck check, string propertyName)
    {
        check.Details.Should().NotBeNull();
        var property = check.Details!.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"details should expose {propertyName}");
        return property!.GetValue(check.Details)?.ToString();
    }

    private static bool? GetDetailsBool(DoctorCheck check, string propertyName)
    {
        check.Details.Should().NotBeNull();
        var property = check.Details!.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"details should expose {propertyName}");
        return property!.GetValue(check.Details) as bool?;
    }

    private static int? GetDetailsInt(DoctorCheck check, string propertyName)
    {
        check.Details.Should().NotBeNull();
        var property = check.Details!.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"details should expose {propertyName}");
        return property!.GetValue(check.Details) as int?;
    }

    private static int[] GetDetailsIntArray(DoctorCheck check, string propertyName)
    {
        check.Details.Should().NotBeNull();
        var property = check.Details!.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"details should expose {propertyName}");
        var value = property!.GetValue(check.Details);
        value.Should().BeAssignableTo<IEnumerable<int>>($"details should expose {propertyName} as integer collection");
        return ((IEnumerable<int>)value!).ToArray();
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
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path == "/dev/uinput",
            isLinux: () => true,
            canOpenForRead: path => path == "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
        Assert.Contains("Daemon is not required", readiness.Message);
    }

    [Fact]
    public async Task RunAsync_WhenWaylandWithWritableUInputButNoReadableEventDevice_InputReadinessFails()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key == "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path == "/dev/uinput",
            path => path == "/dev/uinput",
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            isLinux: () => true);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
        Assert.False(GetDetailsBool(readiness, "directFallbackAvailable"));
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
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            isLinux: () => true);

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
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            isLinux: () => true,
            daemonHandshakeProbe: _ => false);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name == "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Fail, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
        Assert.Contains("handshake failed", readiness.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WhenDaemonSocketExistsAndInjectedHandshakeSucceeds_WaylandPasses()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        string? probedSocketPath = null;

        var service = CreateService(
            key => key == "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path == IpcProtocol.DefaultSocketPath,
            _ => false,
            () => true,
            daemonHandshakeProbe: path =>
            {
                probedSocketPath = path;
                return true;
            });

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        probedSocketPath.Should().Be(IpcProtocol.DefaultSocketPath);

        var handshake = Assert.Single(report.Checks, x => x.Name == "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Pass, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
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
            isLinux: () => true,
            daemonHandshakeProbe: _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => []);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name == "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Fail, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
    }

    [Fact]
    public async Task RunAsync_WhenGsrVirtualKeyboardDetected_AddsCompatibilityWarning()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key == "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path == "/dev/uinput",
            isLinux: () => true,
            readAllTextIfExists: path => path == "/proc/bus/input/devices"
                ? "N: Name=\"gsr-ui virtual keyboard\"\nH: Handlers=sysrq kbd event25\n"
                : null,
            canOpenForRead: path => path == "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var gsr = Assert.Single(report.Checks, x => x.Name == "linux-gsr-compatibility");
        Assert.Equal(DoctorCheckStatus.Warn, gsr.Status);
        Assert.Contains("GPU Screen Recorder", gsr.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(GetDetailsBool(gsr, "gsrVirtualKeyboardDetected"));
        Assert.Equal(LinuxGsrCompatibility.VirtualKeyboardName, GetDetailsString(gsr, "matchedDeviceName"));
    }

    [Fact]
    public async Task RunAsync_WhenGsrVirtualKeyboardNotDetected_AddsCompatibilityPass()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key == "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path == "/dev/uinput",
            isLinux: () => true,
            readAllTextIfExists: path => path == "/proc/bus/input/devices"
                ? "N: Name=\"AT Translated Set 2 keyboard\"\nH: Handlers=sysrq kbd event3\n"
                : null,
            canOpenForRead: path => path == "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var gsr = Assert.Single(report.Checks, x => x.Name == "linux-gsr-compatibility");
        Assert.Equal(DoctorCheckStatus.Pass, gsr.Status);
        Assert.False(GetDetailsBool(gsr, "gsrVirtualKeyboardDetected"));
    }

    [Fact]
    public async Task RunAsync_WhenInputDevicesProcCannotBeRead_DoesNotWarnForGsr()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key == "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path == "/dev/uinput",
            isLinux: () => true,
            readAllTextIfExists: _ => null,
            canOpenForRead: path => path == "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var gsr = Assert.Single(report.Checks, x => x.Name == "linux-gsr-compatibility");
        Assert.Equal(DoctorCheckStatus.Pass, gsr.Status);
        Assert.False(GetDetailsBool(gsr, "gsrVirtualKeyboardDetected"));
    }

    [Fact]
    public async Task RunAsync_WhenIssue44SocketPermissionDeniedScenario_WaylandReadinessFailsWithoutDirectFallback()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = Issue44DoctorScenario.SocketPermissionDenied();

        var service = CreateService(
            scenario.GetEnvironmentVariable,
            scenario.FileExists,
            scenario.CanOpenForWrite,
            isLinux: () => true,
            daemonHandshakeProbe: scenario.ProbeDaemonHandshake,
            daemonSocketAccessProbe: scenario.ProbeDaemonSocketAccess,
            daemonHandshakeDiagnosticProbe: scenario.ProbeDaemonHandshakeDiagnostic,
            canOpenForRead: scenario.CanOpenForRead,
            getInputEventCandidates: scenario.GetInputEventCandidates);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name == "linux-daemon-handshake");
        Assert.Equal(scenario.ExpectedHandshakeStatus, handshake.Status);
        Assert.Equal("PermissionDenied", GetDetailsString(handshake, "failureKind"));
        Assert.False(GetDetailsBool(handshake, "directFallbackAvailable"));

        var access = Assert.Single(report.Checks, x => x.Name == "linux-daemon-access");
        Assert.Equal(DoctorCheckStatus.Fail, access.Status);
        Assert.Equal("PermissionDenied", GetDetailsString(access, "socketStatus"));
        Assert.Equal(1000, GetDetailsInt(access, "currentUid"));
        Assert.Equal([1000, 4242], GetDetailsIntArray(access, "currentProcessGroups"));

        var group = Assert.Single(report.Checks, x => x.Name == "linux-daemon-group");
        Assert.Equal(DoctorCheckStatus.Fail, group.Status);
        Assert.Equal("UserNotMember", GetDetailsString(group, "failureKind"));
        Assert.Contains("usermod", GetDetailsString(group, "remediation"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1000, GetDetailsInt(group, "currentUid"));
        Assert.Equal([1000, 4242], GetDetailsIntArray(group, "currentProcessGroups"));

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(scenario.ExpectedReadinessStatus, readiness.Status);
    }

    [Fact]
    public async Task RunAsync_WhenIssue44DirectFallbackAvailableScenario_WaylandReadinessPassesWithoutDaemon()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = Issue44DoctorScenario.DirectFallbackAvailable();

        var service = CreateService(
            scenario.GetEnvironmentVariable,
            scenario.FileExists,
            scenario.CanOpenForWrite,
            isLinux: () => true,
            daemonHandshakeProbe: scenario.ProbeDaemonHandshake,
            daemonSocketAccessProbe: scenario.ProbeDaemonSocketAccess,
            daemonHandshakeDiagnosticProbe: scenario.ProbeDaemonHandshakeDiagnostic,
            canOpenForRead: scenario.CanOpenForRead,
            getInputEventCandidates: scenario.GetInputEventCandidates);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name == "linux-daemon-handshake");
        Assert.Equal(scenario.ExpectedHandshakeStatus, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(scenario.ExpectedReadinessStatus, readiness.Status);
    }

    [Fact]
    public async Task RunAsync_WhenIssue44SocketPermissionDeniedWithDirectFallback_DaemonFailsButInputCanPass()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = Issue44DoctorScenario.SocketPermissionDenied(directFallbackAvailable: true);

        var service = CreateService(
            scenario.GetEnvironmentVariable,
            scenario.FileExists,
            scenario.CanOpenForWrite,
            isLinux: () => true,
            daemonHandshakeProbe: scenario.ProbeDaemonHandshake,
            daemonSocketAccessProbe: scenario.ProbeDaemonSocketAccess,
            daemonHandshakeDiagnosticProbe: scenario.ProbeDaemonHandshakeDiagnostic,
            canOpenForRead: scenario.CanOpenForRead,
            getInputEventCandidates: scenario.GetInputEventCandidates);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var access = Assert.Single(report.Checks, x => x.Name == "linux-daemon-access");
        Assert.Equal(DoctorCheckStatus.Warn, access.Status);
        Assert.Equal("PermissionDenied", GetDetailsString(access, "socketStatus"));
        Assert.True(GetDetailsBool(access, "directFallbackAvailable"));

        var handshake = Assert.Single(report.Checks, x => x.Name == "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Warn, handshake.Status);
        Assert.Equal("PermissionDenied", GetDetailsString(handshake, "failureKind"));
        Assert.Equal(1000, GetDetailsInt(handshake, "currentUid"));
        Assert.Equal([1000, 4242], GetDetailsIntArray(handshake, "currentProcessGroups"));

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
    }

    [Fact]
    public async Task RunAsync_WhenDaemonGroupIsStale_ReportsReloginRemediation()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = Issue44DoctorScenario.SocketAccessible(
            LinuxDaemonGroupMembershipStatus.StaleSession,
            handshakeStatus: LinuxDaemonHandshakeStatus.PermissionDenied,
            directFallbackAvailable: false);

        var service = CreateService(
            scenario.GetEnvironmentVariable,
            scenario.FileExists,
            scenario.CanOpenForWrite,
            isLinux: () => true,
            daemonHandshakeProbe: scenario.ProbeDaemonHandshake,
            daemonSocketAccessProbe: scenario.ProbeDaemonSocketAccess,
            daemonHandshakeDiagnosticProbe: scenario.ProbeDaemonHandshakeDiagnostic,
            canOpenForRead: scenario.CanOpenForRead,
            getInputEventCandidates: scenario.GetInputEventCandidates);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var group = Assert.Single(report.Checks, x => x.Name == "linux-daemon-group");
        Assert.Equal(DoctorCheckStatus.Fail, group.Status);
        Assert.Equal("StaleSession", GetDetailsString(group, "failureKind"));
        var remediation = GetDetailsString(group, "remediation");
        Assert.Contains("Log out", remediation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reboot", remediation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("usermod", remediation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WhenSocketMissingButDirectFallbackAvailable_SeparatesDaemonWarningFromReadinessPass()
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = Issue44DoctorScenario.DirectFallbackAvailable();

        var service = CreateService(
            scenario.GetEnvironmentVariable,
            scenario.FileExists,
            scenario.CanOpenForWrite,
            isLinux: () => true,
            daemonHandshakeProbe: scenario.ProbeDaemonHandshake,
            daemonSocketAccessProbe: scenario.ProbeDaemonSocketAccess,
            daemonHandshakeDiagnosticProbe: scenario.ProbeDaemonHandshakeDiagnostic,
            canOpenForRead: scenario.CanOpenForRead,
            getInputEventCandidates: scenario.GetInputEventCandidates);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var access = Assert.Single(report.Checks, x => x.Name == "linux-daemon-access");
        Assert.Equal(DoctorCheckStatus.Warn, access.Status);
        Assert.Equal("Missing", GetDetailsString(access, "socketStatus"));
        Assert.True(GetDetailsBool(access, "directFallbackAvailable"));

        var readiness = Assert.Single(report.Checks, x => x.Name == "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
    }

    [Theory]
    [InlineData(LinuxDaemonHandshakeStatus.Timeout, "Timeout")]
    [InlineData(LinuxDaemonHandshakeStatus.ProtocolMismatch, "ProtocolMismatch")]
    public async Task RunAsync_WhenHandshakeFails_DetailsPreserveDistinctFailureKind(
        LinuxDaemonHandshakeStatus handshakeStatus,
        string expectedFailureKind)
    {
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = Issue44DoctorScenario.SocketAccessible(
            LinuxDaemonGroupMembershipStatus.Member,
            handshakeStatus,
            directFallbackAvailable: false);

        var service = CreateService(
            scenario.GetEnvironmentVariable,
            scenario.FileExists,
            scenario.CanOpenForWrite,
            () => true,
            scenario.ProbeDaemonHandshake,
            scenario.ProbeDaemonSocketAccess,
            scenario.ProbeDaemonHandshakeDiagnostic);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name == "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Fail, handshake.Status);
        Assert.Equal(expectedFailureKind, GetDetailsString(handshake, "failureKind"));
        Assert.Equal(expectedFailureKind, GetDetailsString(handshake, "handshakeStatus"));
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
    public async Task RunAsync_WhenMacOSPermissionCheckerHasSeparateStatus_ReturnsModernPermissionChecks()
    {
        _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.MacOS);
        _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var permissionChecker = new TestMacOSPermissionChecker(
            new MacOSPermissionStatus(
                ListenEventGranted: false,
                PostEventGranted: true,
                AccessibilityGranted: false));

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            isWindows: () => false,
            isMacOS: () => true,
            permissionChecker: permissionChecker);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var inputMonitoring = Assert.Single(report.Checks, x => x.Name == "macos-input-monitoring");
        Assert.Equal(DoctorCheckStatus.Fail, inputMonitoring.Status);
        Assert.Contains("Input Monitoring", inputMonitoring.Message);
        Assert.Contains("capture", inputMonitoring.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Accessibility permission is missing", inputMonitoring.Message);

        var eventPosting = Assert.Single(report.Checks, x => x.Name == "macos-event-posting");
        Assert.Equal(DoctorCheckStatus.Pass, eventPosting.Status);
        Assert.Contains("event posting", eventPosting.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("playback", eventPosting.Message, StringComparison.OrdinalIgnoreCase);

        var accessibility = Assert.Single(report.Checks, x => x.Name == "macos-accessibility");
        Assert.Equal(DoctorCheckStatus.Fail, accessibility.Status);
        Assert.Contains("AX features", accessibility.Message);
    }

    [Fact]
    public async Task RunAsync_WhenMacOSPermissionCheckerDoesNotExposeSeparateStatus_ReportsUnavailableModernChecks()
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

        var inputMonitoring = Assert.Single(report.Checks, x => x.Name == "macos-input-monitoring");
        Assert.Equal(DoctorCheckStatus.Warn, inputMonitoring.Status);
        Assert.Contains("Input Monitoring status is unavailable", inputMonitoring.Message);

        var eventPosting = Assert.Single(report.Checks, x => x.Name == "macos-event-posting");
        Assert.Equal(DoctorCheckStatus.Warn, eventPosting.Status);
        Assert.Contains("event posting status is unavailable", eventPosting.Message);

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

    [Fact]
    public void ProbeDaemonHandshake_WhenLinuxTransportAssemblyIsUnavailable_ReturnsFalse()
    {
        var method = typeof(DoctorService).GetMethod(
            "ProbeDaemonHandshake",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            types: [typeof(string)],
            modifiers: null);

        method.Should().NotBeNull();

        var result = (bool)(method!.Invoke(null, ["/run/crossmacro/nonexistent.sock"]) ?? false);

        result.Should().BeFalse();
    }

    private sealed class TestMacOSPermissionChecker : IMacOSPermissionChecker
    {
        private readonly MacOSPermissionStatus _status;

        public TestMacOSPermissionChecker(MacOSPermissionStatus status)
        {
            _status = status;
        }

        public bool IsSupported => true;
        public bool RequiresStartupPermissionGate => true;

        public MacOSPermissionStatus GetCurrentStatus()
        {
            return _status;
        }

        public bool IsPermissionGranted(MacOSPermissionRequirement requirement)
        {
            return _status.IsGranted(requirement);
        }

        public bool IsListenEventAccessGranted()
        {
            return _status.IsGranted(MacOSPermissionRequirement.ListenEvent);
        }

        public bool IsPostEventAccessGranted()
        {
            return _status.IsGranted(MacOSPermissionRequirement.PostEvent);
        }

        public bool RequestPermission(MacOSPermissionRequirement requirement)
        {
            return false;
        }

        public bool RequestListenEventAccess()
        {
            return false;
        }

        public bool RequestPostEventAccess()
        {
            return false;
        }

        public bool IsAccessibilityTrusted()
        {
            return _status.AccessibilityGranted;
        }

        public bool CheckUInputAccess()
        {
            return false;
        }

        public void OpenAccessibilitySettings()
        {
        }

        public void OpenInputMonitoringSettings()
        {
        }
    }
}
