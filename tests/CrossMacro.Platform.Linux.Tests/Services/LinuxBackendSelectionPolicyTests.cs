
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxBackendSelectionPolicyTests
{
    [Fact]
    public void SelectInput_BackendIdentityDoesNotDependOnDiagnosticWording()
    {
        var snapshot = CreateSnapshot(CompositorType.X11, daemon: false, directUInput: false, canReadInputEvents: false);
        var selection = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported: true, forCapture: true)
            with
        { Reason = "localized diagnostic", };

        Assert.Equal(LinuxInputBackend.NativeX11, selection.Backend);
        Assert.True(selection.IsSupported);
    }

    [Theory]
    [InlineData(false, true, true, true, false, InputProviderMode.Daemon, true)]
    [InlineData(false, false, true, true, false, InputProviderMode.Legacy, true)]
    [InlineData(false, false, true, false, true, InputProviderMode.None, false)]
    [InlineData(false, false, false, false, false, InputProviderMode.None, false)]
    [InlineData(true, false, false, false, true, InputProviderMode.None, true)]
    public void SelectInput_PreservesBackendPrecedence(
        bool isX11,
        bool daemon,
        bool directUInput,
        bool canReadInputEvents,
        bool forCapture,
        InputProviderMode expectedMode,
        bool expectedSupported)
    {
        var snapshot = CreateSnapshot(
            isX11 ? CompositorType.X11 : CompositorType.Gnome,
            daemon,
            directUInput,
            canReadInputEvents);

        var result = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported: isX11, forCapture);

        Assert.Equal(expectedMode, result.Mode);
        Assert.Equal(expectedSupported, result.IsSupported);
    }

    [Fact]
    public void SelectInput_SimulationKeepsDirectUInputWithoutReadableEvents()
    {
        var snapshot = CreateSnapshot(CompositorType.Gnome, daemon: false, directUInput: true, canReadInputEvents: false) with
        {
            Input = CreateSnapshotInput(
                directUInput: true,
                canReadInputEvents: false,
                daemonStatus: LinuxDaemonHandshakeStatus.MissingSocket,
                resolvedMode: InputProviderMode.Legacy),
        };

        var result = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported: false, forCapture: false);

        Assert.Equal(InputProviderMode.Legacy, result.Mode);
        Assert.True(result.IsSupported);
    }

    [Fact]
    public void SelectInput_ResolvedDirectModeWithoutReadableEvents_RejectsCapture()
    {
        var snapshot = CreateSnapshot(CompositorType.Gnome, daemon: false, directUInput: true, canReadInputEvents: false) with
        {
            Input = CreateSnapshotInput(
                directUInput: true,
                canReadInputEvents: false,
                daemonStatus: LinuxDaemonHandshakeStatus.MissingSocket,
                resolvedMode: InputProviderMode.Legacy),
        };

        var result = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported: false, forCapture: true);

        Assert.Equal(InputProviderMode.None, result.Mode);
        Assert.False(result.IsSupported);
        Assert.Equal("direct-input-events-unavailable", result.Reason);
    }

    [Fact]
    public void SelectInput_UsesResolvedTransientDaemonModeFromSnapshot()
    {
        var snapshot = CreateSnapshot(
            CompositorType.Gnome,
            daemon: false,
            directUInput: false,
            canReadInputEvents: false,
            daemonStatus: LinuxDaemonHandshakeStatus.UnexpectedError) with
        {
            Input = CreateSnapshotInput(
                directUInput: false,
                canReadInputEvents: false,
                daemonStatus: LinuxDaemonHandshakeStatus.UnexpectedError,
                resolvedMode: InputProviderMode.Daemon),
        };

        var result = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported: false, forCapture: false);

        Assert.Equal(InputProviderMode.Daemon, result.Mode);
        Assert.True(result.IsSupported);
    }

    [Theory]
    [InlineData("io.github.alper_han.crossmacro", null)]
    [InlineData(null, "/tmp/CrossMacro.AppImage")]
    public void SelectInput_PortablePackagesIgnoreDaemonAndUseDirectDevices(string? flatpakId, string? appImage)
    {
        var snapshot = CreateSnapshot(
            CompositorType.Kde,
            daemon: true,
            directUInput: true,
            canReadInputEvents: true) with
        {
            Environment = CreateEnvironment(flatpakId, appImage),
            Input = CreateSnapshotInput(
                directUInput: true,
                canReadInputEvents: true,
                daemonStatus: LinuxDaemonHandshakeStatus.Success,
                resolvedMode: InputProviderMode.Daemon),
        };

        var simulation = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported: false, forCapture: false);
        var capture = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported: false, forCapture: true);

        Assert.Equal(InputProviderMode.Legacy, simulation.Mode);
        Assert.Equal(InputProviderMode.Legacy, capture.Mode);
    }

    [Theory]
    [InlineData(LinuxDaemonHandshakeStatus.Success, false, false, InputProviderMode.Daemon, true)]
    [InlineData(LinuxDaemonHandshakeStatus.Success, true, false, InputProviderMode.Daemon, true)]
    [InlineData(LinuxDaemonHandshakeStatus.Timeout, true, true, InputProviderMode.Legacy, true)]
    [InlineData(LinuxDaemonHandshakeStatus.PermissionDenied, false, true, InputProviderMode.None, false)]
    [InlineData(LinuxDaemonHandshakeStatus.MissingSocket, false, false, InputProviderMode.None, false)]
    public void SelectInput_CoversDaemonDirectDeviceAndCaptureAsymmetry(
        LinuxDaemonHandshakeStatus daemonStatus,
        bool directUInput,
        bool canReadInputEvents,
        InputProviderMode expectedMode,
        bool expectedSupported)
    {
        var snapshot = CreateSnapshot(
            CompositorType.Gnome,
            daemon: false,
            directUInput,
            canReadInputEvents,
            daemonStatus);

        var result = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported: false, forCapture: true);

        Assert.Equal(expectedMode, result.Mode);
        Assert.Equal(expectedSupported, result.IsSupported);
    }

    [Theory]
    [InlineData(CompositorType.X11, true, InputProviderMode.None, true)]
    [InlineData(CompositorType.Gnome, true, InputProviderMode.Daemon, true)]
    [InlineData(CompositorType.Kde, false, InputProviderMode.Legacy, true)]
    [InlineData(CompositorType.Sway, false, InputProviderMode.None, false)]
    [InlineData(CompositorType.Unknown, false, InputProviderMode.None, false)]
    public void SelectInput_CoversX11WaylandAndNoBackendCases(
        CompositorType compositor,
        bool nativeX11Supported,
        InputProviderMode expectedMode,
        bool expectedSupported)
    {
        var snapshot = CreateSnapshot(compositor, daemon: compositor is CompositorType.Gnome, directUInput: compositor is CompositorType.Kde, canReadInputEvents: compositor is CompositorType.Kde);

        var result = LinuxBackendSelectionPolicy.SelectInput(snapshot, nativeX11Supported, forCapture: false);

        Assert.Equal(expectedMode, result.Mode);
        Assert.Equal(expectedSupported, result.IsSupported);
    }

    private static LinuxCapabilitySnapshot CreateSnapshot(
        CompositorType compositor,
        bool daemon,
        bool directUInput,
        bool canReadInputEvents,
        LinuxDaemonHandshakeStatus? daemonStatus = null) =>
        new(
            CreateEnvironment(flatpakId: null, appImage: null),
            compositor,
            CreateSnapshotInput(
                directUInput,
                canReadInputEvents,
                daemonStatus ?? (daemon ? LinuxDaemonHandshakeStatus.Success : LinuxDaemonHandshakeStatus.MissingSocket)),
            new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.KWinScreenShot2, ScreenReadErrorKind.BackendUnavailable, "unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.ExtImageCopy, ScreenReadErrorKind.BackendUnavailable, "unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.WlrScreencopy, ScreenReadErrorKind.BackendUnavailable, "unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.Portal, ScreenReadErrorKind.BackendUnavailable, "unavailable")));

    private static LinuxInputCapabilitySnapshot CreateSnapshotInput(
        bool directUInput,
        bool canReadInputEvents,
        LinuxDaemonHandshakeStatus daemonStatus,
        InputProviderMode? resolvedMode = null) =>
        new(
            ResolvedSocketPath: null,
            directUInput,
            canReadInputEvents,
            daemonStatus is LinuxDaemonHandshakeStatus.Success
                ? LinuxDaemonHandshakeProbeResult.Success("/run/crossmacro.sock", TimeSpan.Zero)
                : LinuxDaemonHandshakeProbeResult.Failed("/run/crossmacro.sock", TimeSpan.Zero, daemonStatus),
            resolvedMode);

    private static LinuxEnvironmentSnapshot CreateEnvironment(string? flatpakId, string? appImage) =>
        default(LinuxEnvironmentSnapshot) with
        {
            FlatpakId = flatpakId,
            AppImage = appImage,
        };
}
