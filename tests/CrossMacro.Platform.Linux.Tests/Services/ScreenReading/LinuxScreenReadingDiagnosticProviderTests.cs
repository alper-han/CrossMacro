
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenReadingDiagnosticProviderTests
{
    [Fact]
    public void GetSnapshot_WhenNativeWaylandSelectsWlr_ReportsPolicySelectionAndFallbackReason()
    {
        var provider = CreateProvider(
            isWayland: true,
            isFlatpak: false,
            compositor: CompositorType.Other,
            new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.KWinScreenShot2,
                    ScreenReadErrorKind.BackendUnavailable,
                    "not kde"),
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.ExtImageCopy,
                    ScreenReadErrorKind.BackendUnavailable,
                    "ext protocol missing"),
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal)));

        var snapshot = provider.GetSnapshot();

        Assert.True(snapshot.IsSupportedSession);
        Assert.Equal("Native", snapshot.PolicyName);
        Assert.Equal(["GnomeExtension", "ExtImageCopy", "WlrScreencopy", "Portal"], snapshot.PolicyOrder);
        Assert.Equal("WlrScreencopy", snapshot.SelectedBackend);
        Assert.Contains(snapshot.Backends, backend =>
            backend.Backend is "ExtImageCopy" && backend.ErrorKind is ScreenReadErrorKind.BackendUnavailable && backend.ErrorMessage is "ext protocol missing");
    }

    [Fact]
    public void GetSnapshot_WhenFlatpakWaylandPortalAvailable_ReportsPortalFirstPolicy()
    {
        var provider = CreateProvider(
            isWayland: true,
            isFlatpak: true,
            compositor: CompositorType.KDE,
            new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal)));

        var snapshot = provider.GetSnapshot();

        Assert.Equal("Flatpak", snapshot.PolicyName);
        Assert.Equal(["Portal"], snapshot.PolicyOrder);
        Assert.Equal("Portal", snapshot.SelectedBackend);
    }

    [Fact]
    public void GetSnapshot_WhenPortalHasProviderEvidence_PreservesItForDiagnostics()
    {
        var provider = CreateProvider(
            isWayland: true,
            isFlatpak: true,
            compositor: CompositorType.HYPRLAND,
            new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.KWinScreenShot2, ScreenReadErrorKind.BackendUnavailable, "not kde"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.ExtImageCopy, ScreenReadErrorKind.BackendUnavailable, "not used"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.WlrScreencopy, ScreenReadErrorKind.BackendUnavailable, "not used"),
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal, "Portal ScreenCast provider override: hyprland (hyprland-portals.conf).")));

        var snapshot = provider.GetSnapshot();
        var display = snapshot.ToDisplay();

        Assert.Equal("Portal ScreenCast provider override: hyprland (hyprland-portals.conf).", snapshot.SelectedBackendDetails);
        Assert.Equal(snapshot.SelectedBackendDetails, display.SelectedBackendDetails);
        Assert.Contains(display.Backends, backend => backend.Backend is "Portal" && backend.Details == snapshot.SelectedBackendDetails);
    }

    [Fact]
    public void GetSnapshot_WhenNativeKdeSelectsKWin_ReportsKdePolicy()
    {
        var provider = CreateProvider(
            isWayland: true,
            isFlatpak: false,
            compositor: CompositorType.KDE,
            new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
                LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal)));

        var snapshot = provider.GetSnapshot();

        Assert.Equal("NativeKDE", snapshot.PolicyName);
        Assert.Equal(["KWinScreenShot2", "ExtImageCopy", "WlrScreencopy", "Portal"], snapshot.PolicyOrder);
        Assert.Equal("KWinScreenShot2", snapshot.SelectedBackend);
    }

    [Fact]
    public void GetSnapshot_WhenNativeKdeKWinPermissionDenied_ReportsDesktopEntryRemediation()
    {
        var provider = CreateProvider(
            isWayland: true,
            isFlatpak: false,
            compositor: CompositorType.KDE,
            new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.KWinScreenShot2,
                    ScreenReadErrorKind.PermissionDenied,
                    "kwin denied"),
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.ExtImageCopy,
                    ScreenReadErrorKind.BackendUnavailable,
                    "ext unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.WlrScreencopy,
                    ScreenReadErrorKind.BackendUnavailable,
                    "wlr unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.Portal,
                    ScreenReadErrorKind.BackendUnavailable,
                    "portal unavailable")));

        var snapshot = provider.GetSnapshot();

        Assert.Null(snapshot.SelectedBackend);
        Assert.Equal("KWinScreenShot2", snapshot.FailureBackend);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, snapshot.FailureKind);
        Assert.Contains("X-KDE-DBUS-Restricted-Interfaces", snapshot.Remediation, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSnapshot_WhenPortalPermissionDenied_ReportsUnavailableReasonAndActionableRemediation()
    {
        var provider = CreateProvider(
            isWayland: true,
            isFlatpak: true,
            compositor: CompositorType.KDE,
            new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.KWinScreenShot2,
                    ScreenReadErrorKind.PermissionDenied,
                    "kwin denied"),
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.ExtImageCopy,
                    ScreenReadErrorKind.BackendUnavailable,
                    "ext unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.WlrScreencopy,
                    ScreenReadErrorKind.BackendUnavailable,
                    "wlr unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.Portal,
                    ScreenReadErrorKind.PermissionDenied,
                    "portal denied")));

        var snapshot = provider.GetSnapshot();

        Assert.Null(snapshot.SelectedBackend);
        Assert.Equal("Portal", snapshot.FailureBackend);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, snapshot.FailureKind);
        Assert.Equal("portal denied", snapshot.FailureMessage);
        Assert.Contains("ScreenCast permission", snapshot.Remediation, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSnapshot_WhenNiriPortalIsUnavailable_ExplainsDmaBufCompatibilityBoundary()
    {
        var provider = CreateProvider(
            isWayland: true,
            isFlatpak: true,
            compositor: CompositorType.NIRI,
            new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.KWinScreenShot2, ScreenReadErrorKind.BackendUnavailable, "not kde"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.ExtImageCopy, ScreenReadErrorKind.BackendUnavailable, "ext unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.WlrScreencopy, ScreenReadErrorKind.BackendUnavailable, "wlr unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.Portal, ScreenReadErrorKind.BackendUnavailable, "portal unavailable")));

        var snapshot = provider.GetSnapshot();

        Assert.Contains("DmaBuf", snapshot.Remediation, StringComparison.Ordinal);
        Assert.Contains("MemFd", snapshot.Remediation, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSnapshot_WhenPortalBackendIsUnavailable_ExplainsExplicitProviderSelection()
    {
        var provider = CreateProvider(
            isWayland: true,
            isFlatpak: true,
            compositor: CompositorType.Other,
            new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.KWinScreenShot2, ScreenReadErrorKind.BackendUnavailable, "not kde"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.ExtImageCopy, ScreenReadErrorKind.BackendUnavailable, "ext unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.WlrScreencopy, ScreenReadErrorKind.BackendUnavailable, "wlr unavailable"),
                LinuxScreenReaderBackendCapability.Unavailable(LinuxScreenReaderBackend.Portal, ScreenReadErrorKind.BackendUnavailable, "portal unavailable")));

        var snapshot = provider.GetSnapshot();

        Assert.Contains("*-portals.conf", snapshot.Remediation, StringComparison.Ordinal);
        Assert.Contains("GTK", snapshot.Remediation, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSnapshot_WhenNativeX11Supported_ReportsNativeX11Selection()
    {
        var environmentDetector = Substitute.For<ILinuxEnvironmentDetector>();
        _ = environmentDetector.IsWayland.Returns(returnThis: false);
        _ = environmentDetector.IsX11.Returns(returnThis: true);
        _ = environmentDetector.DetectedCompositor.Returns(CompositorType.X11);
        var runtimeContext = Substitute.For<IRuntimeContext>();
        var capabilityDetector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        var x11SupportProbe = Substitute.For<IX11ScreenCaptureSupportProbe>();
        _ = x11SupportProbe.ProbeSupport().Returns(X11ScreenCaptureSupportResult.Supported());
        var provider = new LinuxScreenReadingDiagnosticProvider(environmentDetector, runtimeContext, capabilityDetector, x11SupportProbe);

        var snapshot = provider.GetSnapshot();
        var display = snapshot.ToDisplay();

        Assert.True(snapshot.IsSupportedSession);
        Assert.Equal("NativeX11", snapshot.PolicyName);
        Assert.Equal(["X11"], snapshot.PolicyOrder);
        Assert.Equal("X11", snapshot.SelectedBackend);
        Assert.Contains(snapshot.Backends, backend => backend.Backend is "X11" && backend.IsAvailable);
        Assert.Equal("Linux screen reading selects X11 backend (NativeX11 policy).", display.Message);
        _ = capabilityDetector.DidNotReceive().GetSnapshot();
    }

    [Fact]
    public void GetSnapshot_WhenNativeX11Unsupported_ReportsX11Failure()
    {
        var environmentDetector = Substitute.For<ILinuxEnvironmentDetector>();
        _ = environmentDetector.IsWayland.Returns(returnThis: false);
        _ = environmentDetector.IsX11.Returns(returnThis: true);
        _ = environmentDetector.DetectedCompositor.Returns(CompositorType.X11);
        var runtimeContext = Substitute.For<IRuntimeContext>();
        var capabilityDetector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        var x11SupportProbe = Substitute.For<IX11ScreenCaptureSupportProbe>();
        _ = x11SupportProbe.ProbeSupport().Returns(X11ScreenCaptureSupportResult.Unsupported("DISPLAY missing"));
        var provider = new LinuxScreenReadingDiagnosticProvider(environmentDetector, runtimeContext, capabilityDetector, x11SupportProbe);

        var snapshot = provider.GetSnapshot();

        Assert.True(snapshot.IsSupportedSession);
        Assert.Equal("NativeX11", snapshot.PolicyName);
        Assert.Null(snapshot.SelectedBackend);
        Assert.Equal("X11", snapshot.FailureBackend);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, snapshot.FailureKind);
        Assert.Equal("DISPLAY missing", snapshot.FailureMessage);
        Assert.Contains("DISPLAY", snapshot.Remediation, StringComparison.Ordinal);
        _ = capabilityDetector.DidNotReceive().GetSnapshot();
    }

    [Fact]
    public void GetSnapshot_WhenNotWaylandOrX11_DoesNotProbeBackendsAndReportsUnsupportedSession()
    {
        var environmentDetector = Substitute.For<ILinuxEnvironmentDetector>();
        _ = environmentDetector.IsWayland.Returns(returnThis: false);
        _ = environmentDetector.IsX11.Returns(returnThis: false);
        _ = environmentDetector.DetectedCompositor.Returns(CompositorType.Unknown);
        var runtimeContext = Substitute.For<IRuntimeContext>();
        _ = runtimeContext.IsFlatpak.Returns(returnThis: false);
        var capabilityDetector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        var x11SupportProbe = Substitute.For<IX11ScreenCaptureSupportProbe>();
        var provider = new LinuxScreenReadingDiagnosticProvider(environmentDetector, runtimeContext, capabilityDetector, x11SupportProbe);

        var snapshot = provider.GetSnapshot();
        var display = snapshot.ToDisplay();

        Assert.False(snapshot.IsSupportedSession);
        Assert.Equal(ScreenReadErrorKind.Unsupported, snapshot.FailureKind);
        Assert.Contains("Wayland", snapshot.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("X11", snapshot.FailureMessage, StringComparison.Ordinal);
        Assert.False(display.HasSelectedBackend);
        Assert.Equal("Linux screen reading is unavailable because this session is not a supported Wayland or X11 session.", display.Message);
        _ = capabilityDetector.DidNotReceive().GetSnapshot();
        _ = x11SupportProbe.DidNotReceive().ProbeSupport();
    }

    [Fact]
    public void ToDisplay_WhenDiagnosticsContainCapturedContent_RedactsPrivateDetails()
    {
        const string privateFailure = "capture failed at /tmp/capture.png raw RGB(255,0,0) frame bytes SECRET_SCREEN_WORD";
        var provider = CreateProvider(
            isWayland: true,
            isFlatpak: false,
            compositor: CompositorType.Other,
            new LinuxScreenReaderCapabilitySnapshot(
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.KWinScreenShot2,
                    ScreenReadErrorKind.BackendUnavailable,
                    "not kde"),
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.ExtImageCopy,
                    ScreenReadErrorKind.CaptureFailed,
                    privateFailure),
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.WlrScreencopy,
                    ScreenReadErrorKind.CaptureFailed,
                    "frame bytes 01 02 03"),
                LinuxScreenReaderBackendCapability.Unavailable(
                    LinuxScreenReaderBackend.Portal,
                    ScreenReadErrorKind.PermissionDenied,
                    privateFailure)));

        var display = provider.GetSnapshot().ToDisplay();

        Assert.Equal("Details redacted for privacy.", display.FailureMessage);
        Assert.DoesNotContain("SECRET_SCREEN_WORD", display.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(display.Backends, backend =>
            backend.Backend is "Portal" && backend.ErrorMessage is "Details redacted for privacy.");
    }

    private static LinuxScreenReadingDiagnosticProvider CreateProvider(
        bool isWayland,
        bool isFlatpak,
        CompositorType compositor,
        LinuxScreenReaderCapabilitySnapshot capabilitySnapshot)
    {
        var environmentDetector = Substitute.For<ILinuxEnvironmentDetector>();
        _ = environmentDetector.IsWayland.Returns(isWayland);
        _ = environmentDetector.DetectedCompositor.Returns(isWayland ? compositor : CompositorType.X11);
        var runtimeContext = Substitute.For<IRuntimeContext>();
        _ = runtimeContext.IsFlatpak.Returns(isFlatpak);
        var capabilityDetector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        _ = capabilityDetector.GetSnapshot().Returns(capabilitySnapshot);
        var x11SupportProbe = Substitute.For<IX11ScreenCaptureSupportProbe>();

        return new LinuxScreenReadingDiagnosticProvider(environmentDetector, runtimeContext, capabilityDetector, x11SupportProbe);
    }

}
