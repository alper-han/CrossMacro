using System.Text.Json.Nodes;
using CrossMacro.Commands.Diagnostics;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Abstractions.Input.Capture;
using CrossMacro.Platform.Abstractions.Input.Pointer;
using CrossMacro.Platform.Abstractions.Input.Simulation;
using CrossMacro.Platform.Abstractions.Permissions;
using CrossMacro.Platform.Abstractions.Runtime;

namespace CrossMacro.Commands.Tests;

public sealed class DoctorServiceTests : IDisposable
{
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly IDisplaySessionService _displaySessionService;
    private readonly List<TempDirectory> _configDirectories = [];

    public DoctorServiceTests()
    {
        _environmentInfoProvider = Substitute.For<IEnvironmentInfoProvider>();
        _displaySessionService = Substitute.For<IDisplaySessionService>();
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.LinuxWayland);
        _ = _environmentInfoProvider.WindowManagerHandlesCloseButton.Returns(returnThis: false);
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });
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
        bool isMacOS = false,
        IInputSimulator? simulator = null,
        IInputCapture? capture = null,
        IMousePositionProvider? positionProvider = null,
        Func<IInputSimulator>? inputSimulatorFactory = null,
        Func<IInputCapture>? inputCaptureFactory = null,
        IPermissionChecker? permissionChecker = null,
        IMacOSScreenRecordingPermissionProbe? macOSScreenRecordingPermissionProbe = null,
        string? configDirectory = null,
        IPlatformDoctorChecks? platformChecks = null)
    {
        var simulatorInstance = simulator ?? CreateInputSimulator();
        var captureInstance = capture ?? CreateInputCapture();
        var positionProviderInstance = positionProvider ?? CreatePositionProvider();

        return new DoctorService(
            isMacOS ? FakeRuntimeContext.MacOS() : FakeRuntimeContext.Linux(),
            _environmentInfoProvider,
            _displaySessionService,
            inputSimulatorFactory ?? (() => simulatorInstance),
            inputCaptureFactory ?? (() => captureInstance),
            positionProviderInstance,
            new ApplicationPaths(configDirectory ?? CreateConfigDirectory()),
            permissionChecker,
            macOSScreenRecordingPermissionProbe,
            platformChecks);
    }

    private string CreateConfigDirectory()
    {
        var directory = new TempDirectory(create: false);
        _configDirectories.Add(directory);
        return directory.Path;
    }

    public void Dispose()
    {
        foreach (var directory in _configDirectories)
        {
            directory.Dispose();
        }
    }

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

    [Fact]
    public async Task RunAsync_WhenConfigDirectoryResolverIsInjected_ReportsInjectedDirectory()
    {
        var configDirectory = CreateConfigDirectory();

        var service = CreateService(configDirectory: configDirectory);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var check = Assert.Single(report.Checks, x => x.Name is "config-path");
        Assert.Equal(configDirectory, GetDetailsString(check, "configDirectory"));
    }

    [Fact]
    public async Task RunAsync_WhenInjectedConfigDirectoryCannotBeCreated_ReportsFailureDetails()
    {
        using var directory = new TempDirectory();
        var blockerPath = directory.WriteFile("blocker");
        var configDirectory = Path.Combine(blockerPath, "crossmacro-doctor-test-config");
        var service = CreateService(configDirectory: configDirectory);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var check = Assert.Single(report.Checks, x => x.Name is "config-path");
        Assert.Equal(DoctorCheckStatus.Fail, check.Status);
        Assert.Equal(configDirectory, GetDetailsString(check, "configDirectory"));
        Assert.NotNull(check.Details!["error"]);
    }

    [Fact]
    public async Task RunAsync_WhenDisplayUnsupported_ContainsFailCheck()
    {
        _ = _displaySessionService.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = "unsupported";
            return false;
        });

        var service = CreateService();

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        Assert.True(report.HasFailures);
        Assert.Contains(report.Checks, x => x.Name is "display-session" && x.Status is DoctorCheckStatus.Fail);
    }

    [Fact]
    public async Task RunAsync_PreservesBaseCheckOrderAndCapabilitySchema()
    {
        var service = CreateService(
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
        ], report.Checks.Select(check => check.Name), StringComparer.Ordinal);

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
        var service = CreateService();

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        Assert.All(report.Checks.Where(check => check.Name is "input-simulator" or "input-capture" or "position-provider"), check => Assert.Null(check.Details));
    }

    [Fact]
    public async Task RunAsync_WhenAlreadyCancelled_ThrowsWithoutRunningProbes()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        var simulatorFactoryCalled = false;
        var service = CreateService(
            inputSimulatorFactory: () =>
            {
                simulatorFactoryCalled = true;
                return CreateInputSimulator();
            },
            inputCaptureFactory: () => CreateInputCapture(),
            positionProvider: CreatePositionProvider());

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => service.RunAsync(verbose: true, cancellationSource.Token));
        Assert.False(simulatorFactoryCalled);
    }

    [Fact]
    public async Task RunAsync_WhenInputSimulatorFactoryThrows_ReportsProbeFailure()
    {
        var service = CreateService(
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
        var service = CreateService();

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        Assert.Contains(report.Checks, x => x.Name is "display-session" && x.Status is DoctorCheckStatus.Pass);
    }

    [Fact]
    public async Task RunAsync_WhenWindowsAndProvidersSupported_ReturnsCapabilityPassChecks()
    {
        _ = _environmentInfoProvider.CurrentEnvironment.Returns(DisplayEnvironment.Windows);

        var service = CreateService();

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

        var permissionChecker = new TestMacOSPermissionChecker(
            new MacOSPermissionStatus(
                ListenEventGranted: false,
                PostEventGranted: true,
                AccessibilityGranted: false));

        var service = CreateService(
            isMacOS: true,
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

        var permissionChecker = new TestMacOSPermissionChecker(new MacOSPermissionStatus(ListenEventGranted: true, PostEventGranted: true, AccessibilityGranted: true));
        var screenRecordingProbe = new TestMacOSScreenRecordingPermissionProbe(preflightAvailable: true, granted: granted);

        var service = CreateService(
            isMacOS: true,
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

        var permissionChecker = new TestMacOSPermissionChecker(new MacOSPermissionStatus(ListenEventGranted: true, PostEventGranted: true, AccessibilityGranted: true));
        var screenRecordingProbe = new TestMacOSScreenRecordingPermissionProbe(preflightAvailable: false, granted: false);

        var service = CreateService(
            isMacOS: true,
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

        var permissionChecker = Substitute.For<IMacOSPermissionChecker>();
        _ = permissionChecker.IsSupported.Returns(returnThis: true);
        _ = permissionChecker.GetCurrentStatus().Returns(_ => throw new InvalidOperationException("status failed"));
        var screenRecordingProbe = new TestMacOSScreenRecordingPermissionProbe(preflightAvailable: true, granted: true);

        var service = CreateService(
            isMacOS: true,
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

        var permissionChecker = Substitute.For<IPermissionChecker>();
        _ = permissionChecker.IsSupported.Returns(returnThis: true);
        _ = permissionChecker.IsAccessibilityTrusted().Returns(returnThis: false);

        var service = CreateService(
            isMacOS: true,
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

        var service = CreateService(
            simulator: CreateInputSimulator(isSupported: false, providerName: "unsupported-sim"));

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        var simulatorCheck = Assert.Single(report.Checks, x => x.Name is "input-simulator");
        Assert.Equal(DoctorCheckStatus.Fail, simulatorCheck.Status);
        Assert.Contains("unavailable", simulatorCheck.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WhenPlatformChecksAreRegistered_PreparesBeforeTheProbesAndAppendsTheirChecksAfterThem()
    {
        var warning = new DoctorCheck { Name = "platform-warning", Status = DoctorCheckStatus.Warn, Message = "not ready" };
        var contributed = new DoctorCheck { Name = "platform-check", Status = DoctorCheckStatus.Pass, Message = "ok" };
        IReadOnlyList<DoctorCheck>? capabilityChecks = null;
        var platformChecks = Substitute.For<IPlatformDoctorChecks>();
        _ = platformChecks.PrepareAsync(verbose: true, Arg.Any<CancellationToken>()).Returns(warning);
        _ = platformChecks.CheckAsync(Arg.Any<IReadOnlyList<DoctorCheck>>(), verbose: true, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                capabilityChecks = call.Arg<IReadOnlyList<DoctorCheck>>();
                return Task.FromResult<IReadOnlyList<DoctorCheck>>([contributed]);
            });
        var service = CreateService(platformChecks: platformChecks);

        var report = await service.RunAsync(verbose: true, CancellationToken.None);

        Assert.Equal(
        [
            "platform",
            "display-environment",
            "config-path",
            "display-session",
            "platform-warning",
            "input-simulator",
            "input-capture",
            "position-provider",
            "platform-check",
        ], report.Checks.Select(check => check.Name), StringComparer.Ordinal);
        Assert.Equal(["input-simulator", "input-capture", "position-provider"], capabilityChecks!.Select(check => check.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WhenPlatformPreparationHasNothingToReport_AddsNoWarning()
    {
        var platformChecks = Substitute.For<IPlatformDoctorChecks>();
        _ = platformChecks.PrepareAsync(verbose: false, Arg.Any<CancellationToken>()).Returns((DoctorCheck?)null);
        _ = platformChecks.CheckAsync(Arg.Any<IReadOnlyList<DoctorCheck>>(), verbose: false, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DoctorCheck>>([]));
        var service = CreateService(platformChecks: platformChecks);

        var report = await service.RunAsync(verbose: false, CancellationToken.None);

        Assert.Equal(7, report.Checks.Count);
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

        public bool IsScreenRecordingAccessGranted()
        {
            return _status.IsGranted(MacOSPermissionRequirement.ScreenRecording);
        }

        public bool RequestScreenRecordingAccess()
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

        public void OpenScreenRecordingSettings()
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
}
