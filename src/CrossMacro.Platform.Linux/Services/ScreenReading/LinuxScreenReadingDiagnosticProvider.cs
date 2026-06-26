using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Linux.DisplayServer.X11;

namespace CrossMacro.Platform.Linux.Services.ScreenReading;

public sealed class LinuxScreenReadingDiagnosticProvider : IScreenReadingDiagnosticProvider
{
    private readonly ILinuxEnvironmentDetector _environmentDetector;
    private readonly IRuntimeContext _runtimeContext;
    private readonly ILinuxScreenReaderCapabilityDetector _capabilityDetector;
    private readonly IX11ScreenCaptureSupportProbe _x11SupportProbe;

    public LinuxScreenReadingDiagnosticProvider(
        ILinuxEnvironmentDetector environmentDetector,
        IRuntimeContext runtimeContext,
        ILinuxScreenReaderCapabilityDetector capabilityDetector,
        IX11ScreenCaptureSupportProbe x11SupportProbe)
    {
        _environmentDetector = environmentDetector ?? throw new ArgumentNullException(nameof(environmentDetector));
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        _x11SupportProbe = x11SupportProbe ?? throw new ArgumentNullException(nameof(x11SupportProbe));
    }

    public ScreenReadingDiagnosticSnapshot GetSnapshot()
    {
        var order = LinuxScreenReaderBackendPolicy.GetOrder(_runtimeContext.IsFlatpak, _environmentDetector.DetectedCompositor);
        var policyName = LinuxScreenReaderBackendPolicy.GetPolicyName(_runtimeContext.IsFlatpak, _environmentDetector.DetectedCompositor);

        if (_environmentDetector.IsX11)
        {
            return GetX11Snapshot();
        }

        if (!_environmentDetector.IsWayland)
        {
            return new ScreenReadingDiagnosticSnapshot(
                IsSupportedSession: false,
                SessionKind: _environmentDetector.DetectedCompositor.ToString(),
                PolicyName: "UnsupportedLinuxSession",
                PolicyOrder: [],
                SelectedBackend: null,
                Backends: [],
                FailureBackend: null,
                FailureKind: ScreenReadErrorKind.Unsupported,
                FailureMessage: $"Linux screen reading is currently supported on Wayland and native X11. Detected compositor: {_environmentDetector.DetectedCompositor}.",
                Remediation: "Use a Wayland or native X11 desktop session for Linux screen reading.");
        }

        var capabilitySnapshot = _capabilityDetector.GetSnapshot();
        var orderedCapabilities = order.Select(capabilitySnapshot.GetCapability).ToArray();
        var selected = orderedCapabilities.FirstOrDefault(capability => capability.IsAvailable);
        var hasSelected = selected.IsAvailable;
        var failure = hasSelected ? null : SelectFailure(orderedCapabilities);

        return new ScreenReadingDiagnosticSnapshot(
            IsSupportedSession: true,
            SessionKind: _environmentDetector.DetectedCompositor.ToString(),
            PolicyName: policyName,
            PolicyOrder: FormatPolicyOrder(order),
            SelectedBackend: hasSelected ? selected.Backend.ToString() : null,
            Backends: orderedCapabilities.Select(ToDiagnostic).ToArray(),
            FailureBackend: failure?.Backend.ToString(),
            FailureKind: failure?.ErrorKind,
            FailureMessage: failure?.ErrorMessage,
            Remediation: failure is null ? null : GetRemediation(failure.Value));
    }

    private static string[] FormatPolicyOrder(IReadOnlyList<LinuxScreenReaderBackend> order) =>
        order.Select(backend => backend.ToString()).ToArray();

    private ScreenReadingDiagnosticSnapshot GetX11Snapshot()
    {
        var support = _x11SupportProbe.ProbeSupport();
        return new ScreenReadingDiagnosticSnapshot(
            IsSupportedSession: true,
            SessionKind: _environmentDetector.DetectedCompositor.ToString(),
            PolicyName: "NativeX11",
            PolicyOrder: ["X11"],
            SelectedBackend: support.IsSupported ? "X11" : null,
            Backends:
            [
                new ScreenReadingBackendDiagnostic(
                    "X11",
                    support.IsSupported,
                    support.ErrorKind,
                    support.ErrorMessage)
            ],
            FailureBackend: support.IsSupported ? null : "X11",
            FailureKind: support.IsSupported ? null : support.ErrorKind ?? ScreenReadErrorKind.BackendUnavailable,
            FailureMessage: support.IsSupported ? null : support.ErrorMessage ?? "X11 screen reading is unavailable.",
            Remediation: support.IsSupported ? null : "Use a native X11 session with a reachable DISPLAY and libX11 available.");
    }

    private static ScreenReadingBackendDiagnostic ToDiagnostic(LinuxScreenReaderBackendCapability capability) =>
        new(
            capability.Backend.ToString(),
            capability.IsAvailable,
            capability.ErrorKind,
            capability.ErrorMessage);

    private static LinuxScreenReaderBackendCapability? SelectFailure(IReadOnlyList<LinuxScreenReaderBackendCapability> orderedCapabilities)
    {
        var permissionDenied = orderedCapabilities.FirstOrDefault(capability =>
            !capability.IsAvailable && capability.ErrorKind == ScreenReadErrorKind.PermissionDenied);
        if (permissionDenied.ErrorKind == ScreenReadErrorKind.PermissionDenied)
        {
            return permissionDenied;
        }

        return orderedCapabilities.LastOrDefault(capability => !capability.IsAvailable);
    }

    private static string? GetRemediation(LinuxScreenReaderBackendCapability failure)
    {
        if (failure.Backend == LinuxScreenReaderBackend.KWinScreenShot2)
        {
            return "Install a KDE desktop entry for CrossMacro that includes X-KDE-DBUS-Restricted-Interfaces=org.kde.KWin.ScreenShot2, then restart the app.";
        }

        if (failure.ErrorKind == ScreenReadErrorKind.PermissionDenied && failure.Backend == LinuxScreenReaderBackend.Portal)
        {
            return "Grant ScreenCast permission in the desktop portal prompt, or reset portal permissions and retry.";
        }

        if (failure.ErrorKind == ScreenReadErrorKind.PermissionDenied)
        {
            return "Grant the desktop permission requested by the selected screen-reading backend and retry.";
        }

        return failure.Backend switch
        {
            LinuxScreenReaderBackend.GnomeExtension => "Install and enable the CrossMacro GNOME Shell extension, or allow fallback to another backend.",
            LinuxScreenReaderBackend.ExtImageCopy => "Use a compositor that exposes ext-image-copy-capture-v1, or allow fallback to another backend.",
            LinuxScreenReaderBackend.WlrScreencopy => "Use a compositor that exposes wlr-screencopy-unstable-v1, or allow fallback to another backend.",
            LinuxScreenReaderBackend.Portal => "Install and enable XDG Desktop Portal ScreenCast with PipeWire, or use a native Wayland backend.",
            _ => null
        };
    }
}
