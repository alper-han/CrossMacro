using System.Text.Json.Nodes;
using CrossMacro.Platform.Linux.Services.Diagnostics;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxDoctorChecksTests
{
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;

    public LinuxDoctorChecksTests()
    {
        _environmentInfoProvider = Substitute.For<IEnvironmentInfoProvider>();
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.LinuxWayland);
    }

    private static IInputSimulator CreateInputSimulator(bool isSupported = true, string providerName = "test-simulator")
    {
        var simulator = Substitute.For<IInputSimulator>();
        _ = simulator.IsSupported.Returns(isSupported);
        _ = simulator.ProviderName.Returns(providerName);
        return simulator;
    }

    private static IInputCapture CreateInputCapture(bool isSupported = true, string providerName = "test-capture")
    {
        var capture = Substitute.For<IInputCapture>();
        _ = capture.IsSupported.Returns(isSupported);
        _ = capture.ProviderName.Returns(providerName);
        return capture;
    }

    private static DoctorCheck ProbeCheck(string name, Func<bool> isSupported)
    {
        try
        {
            return new DoctorCheck { Name = name, Status = isSupported() ? DoctorCheckStatus.Pass : DoctorCheckStatus.Fail, Message = name };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new DoctorCheck { Name = name, Status = DoctorCheckStatus.Fail, Message = ex.Message };
        }
    }

    private static ScreenReadingDiagnosticSnapshot DefaultScreenReadingSnapshot() => new(
        IsSupportedSession: true,
        SessionKind: "Other",
        PolicyName: "Native",
        PolicyOrder: [],
        SelectedBackend: null,
        Backends: [],
        FailureBackend: null,
        FailureKind: null,
        FailureMessage: null,
        Remediation: null);

    private DoctorRun CreateService(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, CancellationToken, ValueTask<LinuxDaemonSocketAccessResult>>? daemonSocketAccessProbe = null,
        Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult>? daemonHandshakeDiagnosticProbe = null,
        IInputSimulator? simulator = null,
        IInputCapture? capture = null,
        Func<IInputSimulator>? inputSimulatorFactory = null,
        Func<IInputCapture>? inputCaptureFactory = null,
        Func<string, string?>? readAllTextIfExists = null,
        Func<string, bool>? canOpenForRead = null,
        Func<string[]>? getInputEventCandidates = null,
        Func<bool>? hasUsableReadableInputDevices = null,
        IScreenReadingDiagnosticProvider? screenReadingDiagnosticProvider = null,
        IScreenReadingCapabilityReadiness? screenReadingCapabilityReadiness = null,
        bool linuxDaemonDiagnosticsEnabled = true,
        LinuxEnvironmentSnapshot? environment = null)
    {
        var simulatorInstance = simulator ?? CreateInputSimulator();
        var captureInstance = capture ?? CreateInputCapture();
        var inputEventCandidates = getInputEventCandidates ?? (() => []);
        var readable = canOpenForRead ?? (_ => false);
        var linuxInputDiagnostics = new FakeLinuxInputDiagnostics(
            linuxDaemonDiagnosticsEnabled,
            fileExists,
            canOpenForWrite,
            hasUsableReadableInputDevices ?? (() => inputEventCandidates().Any(readable)),
            readAllTextIfExists ?? (_ => null),
            daemonSocketAccessProbe ?? ((socketPath, _) => ValueTask.FromResult(
                fileExists(socketPath)
                    ? new LinuxDaemonSocketAccessResult(socketPath, LinuxDaemonSocketAccessStatus.UnexpectedError, Message: "Socket probe failed.")
                    : LinuxDaemonSocketAccessResult.Missing(socketPath))),
            daemonHandshakeDiagnosticProbe ?? ((socketPath, timeout) => LinuxDaemonHandshakeProbeResult.Failed(
                socketPath,
                timeout,
                LinuxDaemonHandshakeStatus.UnexpectedError,
                "Handshake probe failed.")));
        var screenReadingReadiness = screenReadingCapabilityReadiness ?? Substitute.For<IScreenReadingCapabilityReadiness>();

        var runtimeContext = new LinuxRuntimeContext(environment ?? default(LinuxEnvironmentSnapshot) with
        {
            SessionType = getEnvironmentVariable("XDG_SESSION_TYPE"),
            Display = getEnvironmentVariable("DISPLAY"),
            WaylandDisplay = getEnvironmentVariable("WAYLAND_DISPLAY"),
        });

        var checks = new LinuxDoctorChecks(
            runtimeContext,
            _environmentInfoProvider,
            linuxInputDiagnostics,
            screenReadingDiagnosticProvider ?? new TestScreenReadingDiagnosticProvider(DefaultScreenReadingSnapshot()),
            screenReadingReadiness);
        return new DoctorRun(
            checks,
            () => ProbeCheck("input-simulator", () =>
            {
                using var instance = (inputSimulatorFactory ?? (() => simulatorInstance))();
                return instance.IsSupported;
            }),
            () => ProbeCheck("input-capture", () =>
            {
                using var instance = (inputCaptureFactory ?? (() => captureInstance))();
                return instance.IsSupported;
            }));
    }

    private DoctorRun CreateService(LinuxDoctorInputScenario scenario) => CreateService(
        LinuxDoctorInputScenario.GetEnvironmentVariable,
        scenario.FileExists,
        scenario.CanOpenForWrite,
        daemonSocketAccessProbe: scenario.ProbeDaemonSocketAccessAsync,
        daemonHandshakeDiagnosticProbe: scenario.ProbeDaemonHandshakeDiagnostic,
        canOpenForRead: scenario.CanOpenForRead,
        getInputEventCandidates: scenario.GetInputEventCandidates);

    private static JsonNode GetDetailsNode(DoctorCheck check, string propertyName)
    {
        Assert.NotNull(check.Details);
        var node = check.Details[propertyName];
        Assert.NotNull(node);
        return node;
    }

    private static string? GetDetailsString(DoctorCheck check, string propertyName) =>
        GetDetailsNode(check, propertyName).ToString();

    private static bool? GetDetailsBool(DoctorCheck check, string propertyName) =>
        GetDetailsNode(check, propertyName).GetValue<bool>();

    private static int? GetDetailsInt(DoctorCheck check, string propertyName) =>
        GetDetailsNode(check, propertyName).GetValue<int>();

    private static int[] GetDetailsIntArray(DoctorCheck check, string propertyName) =>
        GetDetailsNode(check, propertyName).AsArray().Select(x => x!.GetValue<int>()).ToArray();

    private static string[] GetDetailsStringArray(DoctorCheck check, string propertyName) =>
        GetDetailsNode(check, propertyName).AsArray().Select(x => x!.GetValue<string>()).ToArray();

    [Fact]
    public async Task Checks_WhenLinuxScreenReadingReadinessIsRegistered_AwaitsItBeforeProbing()
    {
        var readiness = Substitute.For<IScreenReadingCapabilityReadiness>();
        _ = readiness.EnsureReadyAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            screenReadingCapabilityReadiness: readiness);

        _ = await service.RunAsync(verbose: true, CancellationToken.None);

        _ = readiness.Received(1).EnsureReadyAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Checks_WhenLinuxScreenReadingInitializationFails_ReportsWarningAndContinues(bool verbose)
    {
        var readiness = Substitute.For<IScreenReadingCapabilityReadiness>();
        _ = readiness.EnsureReadyAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("screen-reading initialization failed")));
        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            screenReadingCapabilityReadiness: readiness);

        var report = await service.RunAsync(verbose, CancellationToken.None);

        var warning = Assert.Single(report.Checks, check => check.Name is "linux-screen-reading-initialization");
        Assert.Equal(DoctorCheckStatus.Warn, warning.Status);
        Assert.Contains(report.Checks, check => check.Name is "input-simulator");
        Assert.Contains(report.Checks, check => check.Name is "input-capture");
        if (verbose)
        {
            Assert.Equal("screen-reading initialization failed", GetDetailsString(warning, "error"));
        }
        else
        {
            Assert.Null(warning.Details);
        }
    }

    [Fact]
    public async Task Checks_ReportTheSessionTypeAndDisplayVariablesOfTheRuntimeContext()
    {
        var service = CreateService(
            key => key switch
            {
                "XDG_SESSION_TYPE" => "wayland",
                "WAYLAND_DISPLAY" => "wayland-0",
                _ => null,
            },
            _ => false,
            _ => false);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var variables = Assert.Single(report.Checks, check => check.Name is "linux-display-vars");
        Assert.Equal(DoctorCheckStatus.Pass, variables.Status);
        Assert.Equal("wayland", GetDetailsString(variables, "xdgSessionType"));
        Assert.Equal("wayland-0", GetDetailsString(variables, "waylandDisplay"));
    }

    [Fact]
    public async Task Checks_WhenTheSessionProvidesNoTypeOrDisplay_WarnsAboutDisplayVariables()
    {
        var service = CreateService(
            _ => null,
            _ => false,
            _ => false);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        var variables = Assert.Single(report.Checks, check => check.Name is "linux-display-vars");
        Assert.Equal(DoctorCheckStatus.Warn, variables.Status);
    }

    [Fact]
    public async Task Checks_WhenLinuxDaemonSocketProbeIsPending_PropagatesCancellation()
    {
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationSource = new CancellationTokenSource();
        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            daemonSocketAccessProbe: async (_, cancellationToken) =>
            {
                probeStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken);
                return LinuxDaemonSocketAccessResult.Missing(IpcProtocol.DefaultSocketPath);
            });

        var runTask = service.RunAsync(verbose: true, cancellationSource.Token);
        await probeStarted.Task;
        await cancellationSource.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Theory]
    [InlineData(true, true, false, DoctorCheckStatus.Pass)]
    [InlineData(true, true, true, DoctorCheckStatus.Pass)]
    [InlineData(false, true, false, DoctorCheckStatus.Warn)]
    [InlineData(true, false, false, DoctorCheckStatus.Warn)]
    [InlineData(false, true, true, DoctorCheckStatus.Warn)]
    [InlineData(true, false, true, DoctorCheckStatus.Warn)]
    [InlineData(false, false, false, DoctorCheckStatus.Warn)]
    [InlineData(false, false, true, DoctorCheckStatus.Warn)]
    public async Task Checks_WhenX11_UsesProbedInputCapabilitiesForReadiness(
        bool simulatorSupported,
        bool captureSupported,
        bool directDevicesAvailable,
        DoctorCheckStatus expectedStatus)
    {
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.LinuxX11);
        var service = CreateService(
            key => key switch
            {
                "XDG_SESSION_TYPE" => "x11",
                "DISPLAY" => ":0",
                _ => null,
            },
            path => path is "/dev/uinput" or "/dev/input/event0",
            _ => directDevicesAvailable,
            simulator: CreateInputSimulator(simulatorSupported, "X11 (XTest)"),
            capture: CreateInputCapture(captureSupported, "X11 Facade (Abs/Raw)"),
            canOpenForRead: _ => directDevicesAvailable,
            getInputEventCandidates: () => ["/dev/input/event0"],
            linuxDaemonDiagnosticsEnabled: false);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, check => check.Name is "linux-input-readiness");
        Assert.Equal(expectedStatus, readiness.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Checks_WhenX11InputProbeThrows_DoesNotReportInputReady(bool simulatorFails)
    {
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.LinuxX11);
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "x11" : null,
            _ => true,
            _ => true,
            inputSimulatorFactory: simulatorFails ? () => throw new InvalidOperationException("X11 connection failed") : null,
            inputCaptureFactory: simulatorFails ? null : () => throw new InvalidOperationException("X11 connection failed"),
            canOpenForRead: _ => true,
            getInputEventCandidates: () => ["/dev/input/event0"],
            linuxDaemonDiagnosticsEnabled: false);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, check => check.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Warn, readiness.Status);
        var failedProbe = simulatorFails ? "input-simulator" : "input-capture";
        Assert.Contains(report.Checks, check => string.Equals(check.Name, failedProbe, StringComparison.Ordinal) && check.Status is DoctorCheckStatus.Fail);
    }

    [Fact]
    public async Task Checks_WhenWaylandWithWritableUInput_InputReadinessPassesWithoutDaemon()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
        Assert.Contains("Daemon is not required", readiness.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Checks_WhenWaylandWithWritableUInputButNoReadableEventDevice_InputReadinessFails()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput",
            path => path is "/dev/uinput",
            canOpenForRead: _ => false,
            getInputEventCandidates: () => []);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
        Assert.False(GetDetailsBool(readiness, "directFallbackAvailable"));
    }

    [Fact]
    public async Task Checks_WhenWaylandHasRawReadableEventButNoUsableInputDevice_InputReadinessFails()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"],
            hasUsableReadableInputDevices: () => false);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
        Assert.False(GetDetailsBool(readiness, "directFallbackAvailable"));
    }

    [Fact]
    public async Task Checks_WhenWaylandWithoutDaemonAndUInput_InputReadinessFails()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            _ => false,
            _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => []);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
    }

    [Fact]
    public async Task Checks_WhenDaemonSocketExistsButHandshakeFails_AndNoUInput_WaylandFails()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/run/crossmacro/crossmacro.sock",
            _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => []);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Fail, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
        Assert.Contains("handshake failed", readiness.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Checks_WhenDaemonSocketExistsAndInjectedHandshakeSucceeds_WaylandPasses()
    {
        string? probedSocketPath = null;

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            _ => false,
            daemonSocketAccessProbe: (path, _) => ValueTask.FromResult(LinuxDaemonSocketAccessResult.Accessible(path)),
            daemonHandshakeDiagnosticProbe: (path, timeout) =>
            {
                probedSocketPath = path;
                return LinuxDaemonHandshakeProbeResult.Success(path, timeout);
            });

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        Assert.Equal(IpcProtocol.DefaultSocketPath, probedSocketPath);

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Pass, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
    }

    [Fact]
    public async Task Checks_WhenPortableDirectInputIsSelected_SkipsDaemonDiagnostics()
    {
        var socketProbeCount = 0;
        var diagnosticProbeCount = 0;

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            daemonSocketAccessProbe: (socketPath, _) =>
            {
                socketProbeCount++;
                return ValueTask.FromResult(LinuxDaemonSocketAccessResult.Accessible(socketPath));
            },
            daemonHandshakeDiagnosticProbe: (socketPath, timeout) =>
            {
                diagnosticProbeCount++;
                return LinuxDaemonHandshakeProbeResult.Success(socketPath, timeout);
            },
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"],
            linuxDaemonDiagnosticsEnabled: false);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        Assert.Equal(0, socketProbeCount);
        Assert.Equal(0, diagnosticProbeCount);
        Assert.DoesNotContain(report.Checks, check => check.Name.StartsWith("linux-daemon-", StringComparison.Ordinal));
        var readiness = Assert.Single(report.Checks, check => check.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
        Assert.Equal("Wayland input is ready via direct devices.", readiness.Message);
    }

    [Fact]
    public async Task Checks_WhenNativeDaemonDiagnosticsAreSelected_ProbesDaemon()
    {
        var socketProbeCount = 0;
        var diagnosticProbeCount = 0;

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            daemonSocketAccessProbe: (socketPath, _) =>
            {
                socketProbeCount++;
                return ValueTask.FromResult(LinuxDaemonSocketAccessResult.Accessible(socketPath));
            },
            daemonHandshakeDiagnosticProbe: (socketPath, timeout) =>
            {
                diagnosticProbeCount++;
                return LinuxDaemonHandshakeProbeResult.Success(socketPath, timeout);
            },
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        Assert.Equal(1, socketProbeCount);
        Assert.Equal(1, diagnosticProbeCount);
        _ = Assert.Single(report.Checks, check => check.Name is "linux-daemon-socket");
        _ = Assert.Single(report.Checks, check => check.Name is "linux-daemon-handshake");
    }

    [Fact]
    public async Task Checks_WhenDaemonHandshakeFailsAndNoEventDeviceIsReadable_FailsHandshakeAndReadinessDespiteWritableUInput()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/run/crossmacro/crossmacro.sock" or "/dev/uinput",
            path => path is "/dev/uinput",
            canOpenForRead: _ => false,
            getInputEventCandidates: () => []);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Fail, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
    }

    [Fact]
    public async Task Checks_WhenGsrVirtualKeyboardDetected_AddsCompatibilityWarning()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            readAllTextIfExists: path => path is "/proc/bus/input/devices"
                ? "N: Name=\"gsr-ui virtual keyboard\"\nH: Handlers=sysrq kbd event25\n"
                : null,
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var gsr = Assert.Single(report.Checks, x => x.Name is "linux-gsr-compatibility");
        Assert.Equal(DoctorCheckStatus.Warn, gsr.Status);
        Assert.Contains("GPU Screen Recorder", gsr.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(GetDetailsBool(gsr, "gsrVirtualKeyboardDetected"));
        Assert.Equal(LinuxGsrCompatibility.VirtualKeyboardName, GetDetailsString(gsr, "matchedDeviceName"));
    }

    [Fact]
    public async Task Checks_WhenGsrVirtualKeyboardNotDetected_AddsCompatibilityPass()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            readAllTextIfExists: path => path is "/proc/bus/input/devices"
                ? "N: Name=\"AT Translated Set 2 keyboard\"\nH: Handlers=sysrq kbd event3\n"
                : null,
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var gsr = Assert.Single(report.Checks, x => x.Name is "linux-gsr-compatibility");
        Assert.Equal(DoctorCheckStatus.Pass, gsr.Status);
        Assert.False(GetDetailsBool(gsr, "gsrVirtualKeyboardDetected"));
    }

    [Fact]
    public async Task Checks_WhenInputDevicesProcCannotBeRead_DoesNotWarnForGsr()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            readAllTextIfExists: _ => null,
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var gsr = Assert.Single(report.Checks, x => x.Name is "linux-gsr-compatibility");
        Assert.Equal(DoctorCheckStatus.Pass, gsr.Status);
        Assert.False(GetDetailsBool(gsr, "gsrVirtualKeyboardDetected"));
    }

    [Fact]
    public async Task Checks_WhenSocketPermissionDeniedWithoutDirectFallback_WaylandReadinessFails()
    {
        var scenario = LinuxDoctorInputScenario.SocketPermissionDenied();

        var service = CreateService(scenario);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(scenario.ExpectedHandshakeStatus, handshake.Status);
        Assert.Equal("PermissionDenied", GetDetailsString(handshake, "failureKind"));
        Assert.False(GetDetailsBool(handshake, "directFallbackAvailable"));

        var access = Assert.Single(report.Checks, x => x.Name is "linux-daemon-access");
        Assert.Equal(DoctorCheckStatus.Fail, access.Status);
        Assert.Equal("PermissionDenied", GetDetailsString(access, "socketStatus"));
        Assert.Equal(1000, GetDetailsInt(access, "currentUid"));
        Assert.Equal([1000, 4242], GetDetailsIntArray(access, "currentProcessGroups"));

        var group = Assert.Single(report.Checks, x => x.Name is "linux-daemon-group");
        Assert.Equal(DoctorCheckStatus.Fail, group.Status);
        Assert.Equal("UserNotMember", GetDetailsString(group, "failureKind"));
        Assert.Contains("gpasswd", GetDetailsString(group, "remediation"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1000, GetDetailsInt(group, "currentUid"));
        Assert.Equal([1000, 4242], GetDetailsIntArray(group, "currentProcessGroups"));

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(scenario.ExpectedReadinessStatus, readiness.Status);
    }

    [Fact]
    public async Task Checks_WhenLinuxScreenReadingBackendSelected_ReportsPolicyAndSelectedBackend()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"],
            screenReadingDiagnosticProvider: new TestScreenReadingDiagnosticProvider(new ScreenReadingDiagnosticSnapshot(
                IsSupportedSession: true,
                SessionKind: "Other",
                PolicyName: "Native",
                PolicyOrder: ["ExtImageCopy", "WlrScreencopy", "Portal"],
                SelectedBackend: "WlrScreencopy",
                Backends:
                [
                    new ScreenReadingBackendDiagnostic("ExtImageCopy", IsAvailable: false, ScreenReadErrorKind.BackendUnavailable, "ext unavailable"),
                    new ScreenReadingBackendDiagnostic("WlrScreencopy", IsAvailable: true, ErrorKind: null, ErrorMessage: null),
                    new ScreenReadingBackendDiagnostic("Portal", IsAvailable: false, ScreenReadErrorKind.BackendUnavailable, "portal not needed"),
                ],
                FailureBackend: null,
                FailureKind: null,
                FailureMessage: null,
                Remediation: null)));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var screenReading = Assert.Single(report.Checks, x => x.Name is "linux-screen-reading");
        Assert.Equal(DoctorCheckStatus.Pass, screenReading.Status);
        Assert.Contains("WlrScreencopy", screenReading.Message, StringComparison.Ordinal);
        Assert.Contains("Native", screenReading.Message, StringComparison.Ordinal);
        Assert.Equal("WlrScreencopy", GetDetailsString(screenReading, "selectedBackend"));
        Assert.Equal(["ExtImageCopy", "WlrScreencopy", "Portal"], GetDetailsStringArray(screenReading, "policyOrder"));
    }

    [Fact]
    public async Task Checks_WhenLinuxScreenReadingPortalDenied_ReportsActionablePermissionWarning()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            _ => false,
            _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            screenReadingDiagnosticProvider: new TestScreenReadingDiagnosticProvider(new ScreenReadingDiagnosticSnapshot(
                IsSupportedSession: true,
                SessionKind: "Other",
                PolicyName: "Flatpak",
                PolicyOrder: ["Portal"],
                SelectedBackend: null,
                Backends:
                [
                    new ScreenReadingBackendDiagnostic("Portal", IsAvailable: false, ScreenReadErrorKind.PermissionDenied, "portal denied by user"),
                    new ScreenReadingBackendDiagnostic("ExtImageCopy", IsAvailable: false, ScreenReadErrorKind.BackendUnavailable, "ext unavailable"),
                    new ScreenReadingBackendDiagnostic("WlrScreencopy", IsAvailable: false, ScreenReadErrorKind.BackendUnavailable, "wlr unavailable"),
                ],
                FailureBackend: "Portal",
                FailureKind: ScreenReadErrorKind.PermissionDenied,
                FailureMessage: "portal denied by user",
                Remediation: "Grant ScreenCast permission in the desktop portal prompt, or reset portal permissions and retry.")));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var screenReading = Assert.Single(report.Checks, x => x.Name is "linux-screen-reading");
        Assert.Equal(DoctorCheckStatus.Warn, screenReading.Status);
        Assert.Contains("permission denied", screenReading.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ScreenCast permission", screenReading.Message, StringComparison.Ordinal);
        Assert.Equal("Portal", GetDetailsString(screenReading, "failureBackend"));
        Assert.Equal("PermissionDenied", GetDetailsString(screenReading, "failureKind"));
    }

    [Fact]
    public async Task Checks_WhenLinuxScreenReadingKWinDenied_KeepsBackendNameAndDesktopEntryRemediation()
    {
        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            _ => false,
            _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            screenReadingDiagnosticProvider: new TestScreenReadingDiagnosticProvider(new ScreenReadingDiagnosticSnapshot(
                IsSupportedSession: true,
                SessionKind: "KDE",
                PolicyName: "NativeKDE",
                PolicyOrder: ["KWinScreenShot2", "ExtImageCopy", "WlrScreencopy", "Portal"],
                SelectedBackend: null,
                Backends:
                [
                    new ScreenReadingBackendDiagnostic("KWinScreenShot2", IsAvailable: false, ScreenReadErrorKind.PermissionDenied, "KWin ScreenShot2 permission denied."),
                    new ScreenReadingBackendDiagnostic("ExtImageCopy", IsAvailable: false, ScreenReadErrorKind.BackendUnavailable, "ext unavailable"),
                    new ScreenReadingBackendDiagnostic("WlrScreencopy", IsAvailable: false, ScreenReadErrorKind.BackendUnavailable, "wlr unavailable"),
                    new ScreenReadingBackendDiagnostic("Portal", IsAvailable: false, ScreenReadErrorKind.BackendUnavailable, "portal unavailable"),
                ],
                FailureBackend: "KWinScreenShot2",
                FailureKind: ScreenReadErrorKind.PermissionDenied,
                FailureMessage: "KWin ScreenShot2 permission denied.",
                Remediation: "Install a KDE desktop entry for CrossMacro that includes X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2, then restart the app.")));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var screenReading = Assert.Single(report.Checks, x => x.Name is "linux-screen-reading");
        Assert.Equal(DoctorCheckStatus.Warn, screenReading.Status);
        Assert.Contains("KWinScreenShot2", screenReading.Message, StringComparison.Ordinal);
        Assert.Contains("X-KDE-DBUS-Restricted-Interfaces", screenReading.Message, StringComparison.Ordinal);
        Assert.Equal("KWinScreenShot2", GetDetailsString(screenReading, "failureBackend"));
        Assert.Equal(["KWinScreenShot2", "ExtImageCopy", "WlrScreencopy", "Portal"], GetDetailsStringArray(screenReading, "policyOrder"));
    }

    [Fact]
    public async Task Checks_WhenLinuxScreenReadingDiagnosticsContainCapturedContent_RedactsPrivateDetails()
    {
        const string privateFailure = "portal denied after screenshot /tmp/capture.png raw RGB(255,0,0) frame bytes SECRET_SCREEN_WORD";

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            _ => false,
            _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            screenReadingDiagnosticProvider: new TestScreenReadingDiagnosticProvider(new ScreenReadingDiagnosticSnapshot(
                IsSupportedSession: true,
                SessionKind: "Other",
                PolicyName: "Native",
                PolicyOrder: ["ExtImageCopy", "WlrScreencopy", "Portal"],
                SelectedBackend: null,
                Backends:
                [
                    new ScreenReadingBackendDiagnostic("ExtImageCopy", IsAvailable: false, ScreenReadErrorKind.CaptureFailed, privateFailure),
                    new ScreenReadingBackendDiagnostic("WlrScreencopy", IsAvailable: false, ScreenReadErrorKind.CaptureFailed, "frame bytes 01 02 03"),
                    new ScreenReadingBackendDiagnostic("Portal", IsAvailable: false, ScreenReadErrorKind.PermissionDenied, privateFailure),
                ],
                FailureBackend: "Portal",
                FailureKind: ScreenReadErrorKind.PermissionDenied,
                FailureMessage: privateFailure,
                Remediation: "Grant ScreenCast permission in the desktop portal prompt.")));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);
        var screenReading = Assert.Single(report.Checks, check => check.Name is "linux-screen-reading");
        var details = Assert.IsType<JsonObject>(screenReading.Details);

        Assert.Equal("Details redacted for privacy.", details["failureMessage"]!.GetValue<string>());
        Assert.All(details["backends"]!.AsArray(), backend =>
        {
            var backendDetails = backend!.AsObject();
            Assert.Equal("Details redacted for privacy.", backendDetails["errorMessage"]!.GetValue<string>());
        });
        Assert.DoesNotContain("255,0,0", screenReading.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("frame bytes", screenReading.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/tmp/capture.png", screenReading.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("screenshot", screenReading.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SECRET_SCREEN_WORD", screenReading.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Checks_WhenSocketPermissionDeniedWithDirectFallback_DaemonFailsButInputCanPass()
    {
        var scenario = LinuxDoctorInputScenario.SocketPermissionDenied(directFallbackAvailable: true);

        var service = CreateService(scenario);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var access = Assert.Single(report.Checks, x => x.Name is "linux-daemon-access");
        Assert.Equal(DoctorCheckStatus.Warn, access.Status);
        Assert.Equal("PermissionDenied", GetDetailsString(access, "socketStatus"));
        Assert.True(GetDetailsBool(access, "directFallbackAvailable"));

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Warn, handshake.Status);
        Assert.Equal("PermissionDenied", GetDetailsString(handshake, "failureKind"));
        Assert.Equal(1000, GetDetailsInt(handshake, "currentUid"));
        Assert.Equal([1000, 4242], GetDetailsIntArray(handshake, "currentProcessGroups"));

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
    }

    [Fact]
    public async Task Checks_WhenDaemonGroupIsStale_ReportsReloginRemediation()
    {
        var scenario = LinuxDoctorInputScenario.SocketAccessible(
            LinuxDaemonGroupMembershipStatus.StaleSession,
            handshakeStatus: LinuxDaemonHandshakeStatus.PermissionDenied,
            directFallbackAvailable: false);

        var service = CreateService(scenario);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var group = Assert.Single(report.Checks, x => x.Name is "linux-daemon-group");
        Assert.Equal(DoctorCheckStatus.Fail, group.Status);
        Assert.Equal("StaleSession", GetDetailsString(group, "failureKind"));
        var remediation = GetDetailsString(group, "remediation");
        Assert.Contains("Log out", remediation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reboot", remediation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("usermod", remediation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Checks_WhenSocketMissingButDirectFallbackAvailable_SeparatesDaemonWarningFromReadinessPass()
    {
        var scenario = LinuxDoctorInputScenario.DirectFallbackAvailable();

        var service = CreateService(scenario);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var access = Assert.Single(report.Checks, x => x.Name is "linux-daemon-access");
        Assert.Equal(DoctorCheckStatus.Warn, access.Status);
        Assert.Equal("Missing", GetDetailsString(access, "socketStatus"));
        Assert.True(GetDetailsBool(access, "directFallbackAvailable"));

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
    }

    [Theory]
    [InlineData(LinuxDaemonHandshakeStatus.Timeout, "Timeout")]
    [InlineData(LinuxDaemonHandshakeStatus.ProtocolMismatch, "ProtocolMismatch")]
    public async Task Checks_WhenHandshakeFails_DetailsPreserveDistinctFailureKind(
        LinuxDaemonHandshakeStatus handshakeStatus,
        string expectedFailureKind)
    {
        var scenario = LinuxDoctorInputScenario.SocketAccessible(
            LinuxDaemonGroupMembershipStatus.Member,
            handshakeStatus,
            directFallbackAvailable: false);

        var service = CreateService(scenario);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Fail, handshake.Status);
        Assert.Equal(expectedFailureKind, GetDetailsString(handshake, "failureKind"));
        Assert.Equal(expectedFailureKind, GetDetailsString(handshake, "handshakeStatus"));
    }

    /// <summary>Runs the Linux checks the way the doctor does: prepare, then the probes, then the platform checks.</summary>
    private sealed class DoctorRun(LinuxDoctorChecks checks, Func<DoctorCheck> probeSimulator, Func<DoctorCheck> probeCapture)
    {
        public async Task<DoctorRunReport> RunAsync(bool verbose, CancellationToken cancellationToken)
        {
            var report = new List<DoctorCheck>();
            var warning = await checks.PrepareAsync(verbose, cancellationToken);
            if (warning is not null)
            {
                report.Add(warning);
            }

            DoctorCheck[] capabilityChecks = [probeSimulator(), probeCapture()];
            report.AddRange(capabilityChecks);
            report.AddRange(await checks.CheckAsync(capabilityChecks, verbose, cancellationToken));
            return new DoctorRunReport(report);
        }
    }

    private sealed record DoctorRunReport(IReadOnlyList<DoctorCheck> Checks);

    private sealed class TestScreenReadingDiagnosticProvider(ScreenReadingDiagnosticSnapshot snapshot) : IScreenReadingDiagnosticProvider
    {
        public ScreenReadingDiagnosticSnapshot GetSnapshot() => snapshot;
    }
}
