
namespace CrossMacro.Cli.Tests;

public sealed class DoctorServiceTests
{
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly IDisplaySessionService _displaySessionService;

    public DoctorServiceTests()
    {
        _environmentInfoProvider = Substitute.For<IEnvironmentInfoProvider>();
        _displaySessionService = Substitute.For<IDisplaySessionService>();
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.LinuxWayland);
        _ = _environmentInfoProvider.WindowManagerHandlesCloseButton.Returns(returnThis: false);
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

    private static IMousePositionProvider CreatePositionProvider(bool isSupported = true, string providerName = "test-position")
    {
        var provider = Substitute.For<IMousePositionProvider>();
        _ = provider.IsSupported.Returns(isSupported);
        _ = provider.ProviderName.Returns(providerName);
        return provider;
    }

    private DoctorService CreateService(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<bool>? isLinux = null,
        Func<string, bool>? daemonHandshakeProbe = null,
        Func<string, CancellationToken, ValueTask<LinuxDaemonSocketAccessResult>>? daemonSocketAccessProbe = null,
        Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult>? daemonHandshakeDiagnosticProbe = null,
        Func<bool>? isWindows = null,
        Func<bool>? isMacOS = null,
        IInputSimulator? simulator = null,
        IInputCapture? capture = null,
        IMousePositionProvider? positionProvider = null,
        Func<IInputSimulator>? inputSimulatorFactory = null,
        Func<IInputCapture>? inputCaptureFactory = null,
        IPermissionChecker? permissionChecker = null,
        Func<string, string?>? readAllTextIfExists = null,
        Func<string, bool>? canOpenForRead = null,
        Func<string[]>? getInputEventCandidates = null,
        Func<bool>? hasUsableReadableInputDevices = null,
        IScreenReadingDiagnosticProvider? screenReadingDiagnosticProvider = null,
        IMacOSScreenRecordingPermissionProbe? macOSScreenRecordingPermissionProbe = null,
        Func<string>? getConfigDirectory = null,
        IScreenReadingCapabilityReadiness? screenReadingCapabilityReadiness = null,
        bool linuxDaemonDiagnosticsEnabled = true)
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
            inputSimulatorFactory ?? (() => simulatorInstance),
            inputCaptureFactory ?? (() => captureInstance),
            positionProviderInstance,
            permissionChecker,
            isLinux,
            isWindows,
            isMacOS,
            daemonHandshakeProbe,
            daemonSocketAccessProbe,
            daemonHandshakeDiagnosticProbe,
            readAllTextIfExists ?? (_ => null),
            hasUsableReadableInputDevices,
            screenReadingDiagnosticProvider,
            macOSScreenRecordingPermissionProbe,
            getConfigDirectory,
            screenReadingCapabilityReadiness,
            linuxDaemonDiagnosticsEnabled);
    }

    [Fact]
    public async Task RunAsync_WhenLinuxScreenReadingReadinessIsRegistered_AwaitsItBeforeProbing()
    {
        var readiness = Substitute.For<IScreenReadingCapabilityReadiness>();
        _ = readiness.EnsureReadyAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => true,
            screenReadingCapabilityReadiness: readiness);

        _ = await service.RunAsync(verbose: true, CancellationToken.None);

        _ = readiness.Received(1).EnsureReadyAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenConfigDirectoryResolverIsInjected_ReportsInjectedDirectory()
    {
        const string configDirectory = "/tmp/crossmacro-doctor-test-config";

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            getConfigDirectory: () => configDirectory);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var check = Assert.Single(report.Checks, x => x.Name is "config-path");
        Assert.Equal(configDirectory, GetDetailsString(check, "configDirectory"));
    }

    [Fact]
    public async Task RunAsync_WhenInjectedConfigDirectoryCannotBeCreated_ReportsFailureDetails()
    {
        var blockerPath = Path.GetTempFileName();
        try
        {
            var configDirectory = Path.Combine(blockerPath, "crossmacro-doctor-test-config");
            var service = CreateService(
                _ => null,
                _ => false,
                _ => false,
                isLinux: () => false,
                getConfigDirectory: () => configDirectory);

            var report = await service.RunAsync(verbose: true, CancellationToken.None);

            var check = Assert.Single(report.Checks, x => x.Name is "config-path");
            Assert.Equal(DoctorCheckStatus.Fail, check.Status);
            Assert.Equal(configDirectory, GetDetailsString(check, "configDirectory"));
            Assert.NotNull(check.Details!["error"]);
        }
        finally
        {
            File.Delete(blockerPath);
        }
    }

    private static string? GetDetailsString(DoctorCheck check, string propertyName)
    {
        _ = check.Details.Should().NotBeNull();
        var node = check.Details![propertyName];
        _ = node.Should().NotBeNull($"details should expose {propertyName}");
        return node!.ToString();
    }

    private static bool? GetDetailsBool(DoctorCheck check, string propertyName)
    {
        _ = check.Details.Should().NotBeNull();
        var node = check.Details![propertyName];
        _ = node.Should().NotBeNull($"details should expose {propertyName}");
        return node!.GetValue<bool>();
    }

    private static int? GetDetailsInt(DoctorCheck check, string propertyName)
    {
        _ = check.Details.Should().NotBeNull();
        var node = check.Details![propertyName];
        _ = node.Should().NotBeNull($"details should expose {propertyName}");
        return node!.GetValue<int>();
    }

    private static int[] GetDetailsIntArray(DoctorCheck check, string propertyName)
    {
        _ = check.Details.Should().NotBeNull();
        var node = check.Details![propertyName];
        _ = node.Should().NotBeNull($"details should expose {propertyName}");
        return node!.AsArray().Select(x => x!.GetValue<int>()).ToArray();
    }

    private static string[] GetDetailsStringArray(DoctorCheck check, string propertyName)
    {
        _ = check.Details.Should().NotBeNull();
        var node = check.Details![propertyName];
        _ = node.Should().NotBeNull($"details should expose {propertyName}");
        return node!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
    }

    [Fact]
    public async Task RunAsync_WhenDisplayUnsupported_ContainsFailCheck()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
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
        Assert.Contains(report.Checks, x => x.Name is "display-session" && x.Status is DoctorCheckStatus.Fail);
    }

    [Fact]
    public async Task RunAsync_PreservesBaseCheckOrderAndCapabilitySchema()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
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
            simulator: CreateInputSimulator(providerName: "sim"),
            capture: CreateInputCapture(providerName: "capture"),
            positionProvider: CreatePositionProvider(providerName: "position"));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        Assert.Equal(
        [
            "platform",
            "display-environment",
            "config-path",
            "display-session",
            "input-simulator",
            "input-capture",
            "position-provider",
        ], report.Checks.Select(check => check.Name));

        Assert.Collection(
            report.Checks.Skip(4),
            check =>
            {
                Assert.Equal(DoctorCheckStatus.Pass, check.Status);
                Assert.Equal("Input simulator backend is available (sim).", check.Message);
                Assert.Equal("sim", GetDetailsString(check, "provider"));
                Assert.True(GetDetailsBool(check, "supported"));
            },
            check =>
            {
                Assert.Equal(DoctorCheckStatus.Pass, check.Status);
                Assert.Equal("Input capture backend is available (capture).", check.Message);
                Assert.Equal("capture", GetDetailsString(check, "provider"));
                Assert.True(GetDetailsBool(check, "supported"));
            },
            check =>
            {
                Assert.Equal(DoctorCheckStatus.Pass, check.Status);
                Assert.Equal("Position provider is available (position).", check.Message);
                Assert.Equal("position", GetDetailsString(check, "provider"));
                Assert.True(GetDetailsBool(check, "supported"));
            });
    }

    [Fact]
    public async Task RunAsync_WhenNotVerbose_OmitsCapabilityDetails()
    {
        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            isWindows: () => true,
            isMacOS: () => false);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        Assert.All(report.Checks.Where(check => check.Name is "input-simulator" or "input-capture" or "position-provider"), check => Assert.Null(check.Details));
    }

    [Fact]
    public async Task RunAsync_WhenAlreadyCancelled_ThrowsWithoutRunningProbes()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var simulatorFactoryCalled = false;
        var service = new DoctorService(
            _environmentInfoProvider,
            _displaySessionService,
            _ => null,
            _ => false,
            _ => false,
            canOpenForRead: null,
            getInputEventCandidates: null,
            inputSimulatorFactory: () =>
            {
                simulatorFactoryCalled = true;
                return CreateInputSimulator();
            },
            inputCaptureFactory: () => CreateInputCapture(),
            mousePositionProvider: CreatePositionProvider(),
            isLinux: () => false,
            isWindows: () => true,
            isMacOS: () => false);

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => service.RunAsync(verbose: true, cancellationSource.Token));
        Assert.False(simulatorFactoryCalled);
    }

    [Fact]
    public async Task RunAsync_WhenLinuxDaemonSocketProbeIsPending_PropagatesCancellation()
    {
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationSource = new CancellationTokenSource();
        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => true,
            daemonSocketAccessProbe: async (_, cancellationToken) =>
            {
                probeStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return LinuxDaemonSocketAccessResult.Missing(IpcProtocol.DefaultSocketPath);
            });

        var runTask = service.RunAsync(verbose: true, cancellationSource.Token);
        await probeStarted.Task;
        await cancellationSource.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task RunAsync_WhenInputSimulatorFactoryThrows_ReportsProbeFailure()
    {
        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            inputSimulatorFactory: () => throw new InvalidOperationException("simulator factory failed"));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var check = Assert.Single(report.Checks, check => check.Name is "input-simulator");
        Assert.Equal(DoctorCheckStatus.Fail, check.Status);
        Assert.Equal("Input simulator backend probe failed.", check.Message);
        Assert.Equal("simulator factory failed", GetDetailsString(check, "error"));
    }

    [Fact]
    public async Task RunAsync_WhenInputCaptureFactoryThrows_ReportsProbeFailure()
    {
        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            inputCaptureFactory: () => throw new InvalidOperationException("capture factory failed"));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var check = Assert.Single(report.Checks, check => check.Name is "input-capture");
        Assert.Equal(DoctorCheckStatus.Fail, check.Status);
        Assert.Equal("Input capture backend probe failed.", check.Message);
        Assert.Equal("capture factory failed", GetDetailsString(check, "error"));
    }

    [Fact]
    public async Task RunAsync_WhenInputCaptureIsUnsupported_ReportsUnavailableCapture()
    {
        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            capture: CreateInputCapture(isSupported: false, providerName: "unsupported-capture"));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var check = Assert.Single(report.Checks, check => check.Name is "input-capture");
        Assert.Equal(DoctorCheckStatus.Fail, check.Status);
        Assert.Equal("Input capture backend is unavailable (unsupported-capture).", check.Message);
        Assert.Equal("unsupported-capture", GetDetailsString(check, "provider"));
        Assert.False(GetDetailsBool(check, "supported"));
    }

    [Fact]
    public async Task RunAsync_WhenPositionProviderIsUnsupported_ReportsUnavailableProvider()
    {
        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            positionProvider: CreatePositionProvider(isSupported: false, providerName: "unsupported-position"));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var check = Assert.Single(report.Checks, check => check.Name is "position-provider");
        Assert.Equal(DoctorCheckStatus.Warn, check.Status);
        Assert.Equal("Position provider is unavailable (unsupported-position); absolute replay may downgrade to fallback mode.", check.Message);
        Assert.Equal("unsupported-position", GetDetailsString(check, "provider"));
        Assert.False(GetDetailsBool(check, "supported"));
    }

    [Fact]
    public async Task RunAsync_WhenDisplaySupported_ContainsPassDisplayCheck()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
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

        Assert.Contains(report.Checks, x => x.Name is "display-session" && x.Status is DoctorCheckStatus.Pass);
    }

    [Fact]
    public async Task RunAsync_WhenWaylandWithWritableUInput_InputReadinessPassesWithoutDaemon()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            isLinux: () => true,
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
        Assert.Contains("Daemon is not required", readiness.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenWaylandWithWritableUInputButNoReadableEventDevice_InputReadinessFails()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput",
            path => path is "/dev/uinput",
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            isLinux: () => true);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
        Assert.False(GetDetailsBool(readiness, "directFallbackAvailable"));
    }

    [Fact]
    public async Task RunAsync_WhenWaylandHasRawReadableEventButNoUsableInputDevice_InputReadinessFails()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"],
            hasUsableReadableInputDevices: () => false,
            isLinux: () => true);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
        Assert.False(GetDetailsBool(readiness, "directFallbackAvailable"));
    }


    [Fact]
    public async Task RunAsync_WhenWaylandWithoutDaemonAndUInput_InputReadinessFails()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            _ => false,
            _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            isLinux: () => true);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
    }

    [Fact]
    public async Task RunAsync_WhenDaemonSocketExistsButHandshakeFails_AndNoUInput_WaylandFails()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/run/crossmacro/crossmacro.sock",
            _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            isLinux: () => true,
            daemonHandshakeProbe: _ => false);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Fail, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
        Assert.Contains("handshake failed", readiness.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WhenDaemonSocketExistsAndInjectedHandshakeSucceeds_WaylandPasses()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        string? probedSocketPath = null;

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => string.Equals(path, IpcProtocol.DefaultSocketPath, StringComparison.Ordinal),
            _ => false,
            () => true,
            daemonHandshakeProbe: path =>
            {
                probedSocketPath = path;
                return true;
            });

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        _ = probedSocketPath.Should().Be(IpcProtocol.DefaultSocketPath);

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Pass, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
    }

    [Fact]
    public async Task RunAsync_WhenPortableDirectInputIsSelected_SkipsDaemonDiagnostics()
    {
        var socketProbeCount = 0;
        var handshakeProbeCount = 0;
        var diagnosticProbeCount = 0;

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            isLinux: () => true,
            daemonHandshakeProbe: _ =>
            {
                handshakeProbeCount++;
                return true;
            },
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
        Assert.Equal(0, handshakeProbeCount);
        Assert.Equal(0, diagnosticProbeCount);
        Assert.DoesNotContain(report.Checks, check => check.Name.StartsWith("linux-daemon-", StringComparison.Ordinal));
        var readiness = Assert.Single(report.Checks, check => check.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Pass, readiness.Status);
        Assert.Equal("Wayland input is ready via direct devices.", readiness.Message);
    }

    [Fact]
    public async Task RunAsync_WhenNativeDaemonDiagnosticsAreSelected_ProbesDaemon()
    {
        var socketProbeCount = 0;
        var diagnosticProbeCount = 0;

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            isLinux: () => true,
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
    public async Task RunAsync_WhenDaemonHandshakeFailsButUInputWritable_ReportsWarnForHandshake()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/run/crossmacro/crossmacro.sock" or "/dev/uinput",
            path => path is "/dev/uinput",
            isLinux: () => true,
            daemonHandshakeProbe: _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => []);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Fail, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(DoctorCheckStatus.Fail, readiness.Status);
    }

    [Fact]
    public async Task RunAsync_WhenGsrVirtualKeyboardDetected_AddsCompatibilityWarning()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            isLinux: () => true,
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
    public async Task RunAsync_WhenGsrVirtualKeyboardNotDetected_AddsCompatibilityPass()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            isLinux: () => true,
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
    public async Task RunAsync_WhenInputDevicesProcCannotBeRead_DoesNotWarnForGsr()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            isLinux: () => true,
            readAllTextIfExists: _ => null,
            canOpenForRead: path => path is "/dev/input/event0",
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var gsr = Assert.Single(report.Checks, x => x.Name is "linux-gsr-compatibility");
        Assert.Equal(DoctorCheckStatus.Pass, gsr.Status);
        Assert.False(GetDetailsBool(gsr, "gsrVirtualKeyboardDetected"));
    }

    [Fact]
    public async Task RunAsync_WhenIssue44SocketPermissionDeniedScenario_WaylandReadinessFailsWithoutDirectFallback()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = LinuxDoctorInputScenario.SocketPermissionDenied();

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
    public async Task RunAsync_WhenLinuxScreenReadingBackendSelected_ReportsPolicyAndSelectedBackend()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            path => path is "/dev/uinput" or "/dev/input/event0",
            path => path is "/dev/uinput",
            isLinux: () => true,
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
    public async Task RunAsync_WhenLinuxScreenReadingPortalDenied_ReportsActionablePermissionWarning()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            _ => false,
            _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            isLinux: () => true,
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
    public async Task RunAsync_WhenLinuxScreenReadingKWinDenied_KeepsBackendNameAndDesktopEntryRemediation()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            _ => false,
            _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            isLinux: () => true,
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
    public async Task RunAsync_WhenLinuxScreenReadingDiagnosticsContainCapturedContent_RedactsPrivateDetails()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        const string privateFailure = "portal denied after screenshot /tmp/capture.png raw RGB(255,0,0) frame bytes SECRET_SCREEN_WORD";

        var service = CreateService(
            key => key is "XDG_SESSION_TYPE" ? "wayland" : null,
            _ => false,
            _ => false,
            canOpenForRead: _ => false,
            getInputEventCandidates: () => [],
            isLinux: () => true,
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
        _ = screenReading.Details.Should().NotBeNull();
        var details = screenReading.Details!;

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
        Assert.Contains("Details redacted for privacy", details["failureMessage"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WhenIssue44DirectFallbackAvailableScenario_WaylandReadinessPassesWithoutDaemon()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = LinuxDoctorInputScenario.DirectFallbackAvailable();

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

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(scenario.ExpectedHandshakeStatus, handshake.Status);

        var readiness = Assert.Single(report.Checks, x => x.Name is "linux-input-readiness");
        Assert.Equal(scenario.ExpectedReadinessStatus, readiness.Status);
    }

    [Fact]
    public async Task RunAsync_WhenIssue44SocketPermissionDeniedWithDirectFallback_DaemonFailsButInputCanPass()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = LinuxDoctorInputScenario.SocketPermissionDenied(directFallbackAvailable: true);

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
    public async Task RunAsync_WhenDaemonGroupIsStale_ReportsReloginRemediation()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = LinuxDoctorInputScenario.SocketAccessible(
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

        var group = Assert.Single(report.Checks, x => x.Name is "linux-daemon-group");
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
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = LinuxDoctorInputScenario.DirectFallbackAvailable();

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
    public async Task RunAsync_WhenHandshakeFails_DetailsPreserveDistinctFailureKind(
        LinuxDaemonHandshakeStatus handshakeStatus,
        string expectedFailureKind)
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
        var scenario = LinuxDoctorInputScenario.SocketAccessible(
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

        var handshake = Assert.Single(report.Checks, x => x.Name is "linux-daemon-handshake");
        Assert.Equal(DoctorCheckStatus.Fail, handshake.Status);
        Assert.Equal(expectedFailureKind, GetDetailsString(handshake, "failureKind"));
        Assert.Equal(expectedFailureKind, GetDetailsString(handshake, "handshakeStatus"));
    }

    [Fact]
    public async Task RunAsync_WhenWindowsAndProvidersSupported_ReturnsCapabilityPassChecks()
    {
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.Windows);
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
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

        Assert.Contains(report.Checks, x => x.Name is "input-simulator" && x.Status is DoctorCheckStatus.Pass);
        Assert.Contains(report.Checks, x => x.Name is "input-capture" && x.Status is DoctorCheckStatus.Pass);
        Assert.Contains(report.Checks, x => x.Name is "position-provider" && x.Status is DoctorCheckStatus.Pass);
        Assert.DoesNotContain(report.Checks, x => x.Name is "macos-accessibility");
    }

    [Fact]
    public async Task RunAsync_WhenMacOSPermissionCheckerHasSeparateStatus_ReturnsModernPermissionChecks()
    {
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.MacOS);
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
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

        var inputMonitoring = Assert.Single(report.Checks, x => x.Name is "macos-input-monitoring");
        Assert.Equal(DoctorCheckStatus.Fail, inputMonitoring.Status);
        Assert.Contains("Input Monitoring", inputMonitoring.Message, StringComparison.Ordinal);
        Assert.Contains("capture", inputMonitoring.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Accessibility permission is missing", inputMonitoring.Message, StringComparison.Ordinal);

        var eventPosting = Assert.Single(report.Checks, x => x.Name is "macos-event-posting");
        Assert.Equal(DoctorCheckStatus.Pass, eventPosting.Status);
        Assert.Contains("event posting", eventPosting.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("playback", eventPosting.Message, StringComparison.OrdinalIgnoreCase);

        var accessibility = Assert.Single(report.Checks, x => x.Name is "macos-accessibility");
        Assert.Equal(DoctorCheckStatus.Fail, accessibility.Status);
        Assert.Contains("AX features", accessibility.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, DoctorCheckStatus.Pass)]
    [InlineData(false, DoctorCheckStatus.Fail)]
    public async Task RunAsync_WhenMacOSScreenRecordingProbeAvailable_ReportsScreenRecordingCheck(
        bool granted,
        DoctorCheckStatus expectedStatus)
    {
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.MacOS);
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var permissionChecker = new TestMacOSPermissionChecker(new MacOSPermissionStatus(ListenEventGranted: true, PostEventGranted: true, AccessibilityGranted: true));
        var screenRecordingProbe = new TestMacOSScreenRecordingPermissionProbe(preflightAvailable: true, granted: granted);

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            isWindows: () => false,
            isMacOS: () => true,
            permissionChecker: permissionChecker,
            macOSScreenRecordingPermissionProbe: screenRecordingProbe);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var screenRecording = Assert.Single(report.Checks, x => x.Name is "macos-screen-recording");
        Assert.Equal(expectedStatus, screenRecording.Status);
        Assert.Contains("Screen Recording", screenRecording.Message, StringComparison.Ordinal);
        Assert.Equal(granted, GetDetailsBool(screenRecording, "screenRecordingGranted"));
        Assert.True(GetDetailsBool(screenRecording, "preflightApiAvailable"));

        if (!granted)
        {
            Assert.Contains("System Settings", screenRecording.Message, StringComparison.Ordinal);
            Assert.Contains("restart CrossMacro", screenRecording.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task RunAsync_WhenMacOSScreenRecordingPreflightUnavailable_ReportsWarning()
    {
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.MacOS);
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var permissionChecker = new TestMacOSPermissionChecker(new MacOSPermissionStatus(ListenEventGranted: true, PostEventGranted: true, AccessibilityGranted: true));
        var screenRecordingProbe = new TestMacOSScreenRecordingPermissionProbe(preflightAvailable: false, granted: false);

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            isWindows: () => false,
            isMacOS: () => true,
            permissionChecker: permissionChecker,
            macOSScreenRecordingPermissionProbe: screenRecordingProbe);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var screenRecording = Assert.Single(report.Checks, x => x.Name is "macos-screen-recording");
        Assert.Equal(DoctorCheckStatus.Warn, screenRecording.Status);
        Assert.Contains("preflight API is unavailable", screenRecording.Message, StringComparison.Ordinal);
        Assert.False(GetDetailsBool(screenRecording, "preflightApiAvailable"));
    }

    [Fact]
    public async Task RunAsync_WhenMacOSPermissionStatusProbeFails_StillReportsScreenRecordingCheck()
    {
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.MacOS);
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var permissionChecker = Substitute.For<IMacOSPermissionChecker>();
        _ = permissionChecker.IsSupported.Returns(returnThis: true);
        _ = permissionChecker.GetCurrentStatus().Returns(_ => throw new InvalidOperationException("status failed"));
        var screenRecordingProbe = new TestMacOSScreenRecordingPermissionProbe(preflightAvailable: true, granted: true);

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            isWindows: () => false,
            isMacOS: () => true,
            permissionChecker: permissionChecker,
            macOSScreenRecordingPermissionProbe: screenRecordingProbe);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        Assert.Contains(report.Checks, x => x.Name is "macos-screen-recording" && x.Status is DoctorCheckStatus.Pass);
        Assert.Contains(report.Checks, x => x.Name is "macos-input-monitoring" && x.Status is DoctorCheckStatus.Warn);
    }

    [Fact]
    public async Task RunAsync_WhenMacOSPermissionCheckerDoesNotExposeSeparateStatus_ReportsUnavailableModernChecks()
    {
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.MacOS);
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var permissionChecker = Substitute.For<IPermissionChecker>();
        _ = permissionChecker.IsSupported.Returns(returnThis: true);
        _ = permissionChecker.IsAccessibilityTrusted().Returns(returnThis: false);

        var service = CreateService(
            _ => null,
            _ => false,
            _ => false,
            isLinux: () => false,
            isWindows: () => false,
            isMacOS: () => true,
            permissionChecker: permissionChecker);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var inputMonitoring = Assert.Single(report.Checks, x => x.Name is "macos-input-monitoring");
        Assert.Equal(DoctorCheckStatus.Warn, inputMonitoring.Status);
        Assert.Contains("Input Monitoring status is unavailable", inputMonitoring.Message, StringComparison.Ordinal);

        var eventPosting = Assert.Single(report.Checks, x => x.Name is "macos-event-posting");
        Assert.Equal(DoctorCheckStatus.Warn, eventPosting.Status);
        Assert.Contains("event posting status is unavailable", eventPosting.Message, StringComparison.Ordinal);

        var accessibility = Assert.Single(report.Checks, x => x.Name is "macos-accessibility");
        Assert.Equal(DoctorCheckStatus.Fail, accessibility.Status);
    }

    [Fact]
    public async Task RunAsync_WhenInputSimulatorUnsupported_ReturnsFailSimulatorCheck()
    {
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.Windows);
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
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

        var simulatorCheck = Assert.Single(report.Checks, x => x.Name is "input-simulator");
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
            types: [],
            modifiers: null);

        _ = method.Should().NotBeNull();

        var result = (bool)(method!.Invoke(obj: null, parameters: null) ?? false);

        _ = result.Should().BeFalse();
    }

    private sealed class TestMacOSPermissionChecker(MacOSPermissionStatus status) : IMacOSPermissionChecker
    {
        private readonly MacOSPermissionStatus _status = status;

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

        public bool IsListenEventListedOrGranted()
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

        public ValueTask<bool> CheckUInputAccessAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(false);
        }

        public void OpenAccessibilitySettings()
        {
        }

        public void OpenInputMonitoringSettings()
        {
        }
    }

    private sealed class TestMacOSScreenRecordingPermissionProbe(bool preflightAvailable, bool granted) : IMacOSScreenRecordingPermissionProbe
    {
        private readonly bool _granted = granted;

        public bool IsPreflightAvailable { get; } = preflightAvailable;

        public bool IsGranted()
        {
            return _granted;
        }
    }

    private sealed class TestScreenReadingDiagnosticProvider(ScreenReadingDiagnosticSnapshot snapshot) : IScreenReadingDiagnosticProvider
    {
        private readonly ScreenReadingDiagnosticSnapshot _snapshot = snapshot;

        public ScreenReadingDiagnosticSnapshot GetSnapshot()
        {
            return _snapshot;
        }
    }
}
