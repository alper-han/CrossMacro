namespace CrossMacro.Platform.Abstractions.Diagnostics;

public interface IScreenReadingDiagnosticProvider
{
    ScreenReadingDiagnosticSnapshot GetSnapshot();
}

public sealed record ScreenReadingDiagnosticSnapshot(
    bool IsSupportedSession,
    string SessionKind,
    string PolicyName,
    IReadOnlyList<string> PolicyOrder,
    string? SelectedBackend,
    IReadOnlyList<ScreenReadingBackendDiagnostic> Backends,
    string? FailureBackend,
    ScreenReadErrorKind? FailureKind,
    string? FailureMessage,
    string? Remediation);

public sealed record ScreenReadingBackendDiagnostic(
    string Backend,
    bool IsAvailable,
    ScreenReadErrorKind? ErrorKind,
    string? ErrorMessage);

public sealed record ScreenReadingDiagnosticDisplay(
    bool HasSelectedBackend,
    string Message,
    bool IsSupportedSession,
    string? SessionKind,
    string? PolicyName,
    IReadOnlyList<string> PolicyOrder,
    string? SelectedBackend,
    string? FailureBackend,
    string? FailureKind,
    string? FailureMessage,
    string? Remediation,
    IReadOnlyList<ScreenReadingBackendDiagnosticDisplay> Backends);

public sealed record ScreenReadingBackendDiagnosticDisplay(
    string? Backend,
    bool IsAvailable,
    string? ErrorKind,
    string? ErrorMessage);

public static class ScreenReadingDiagnosticDisplayFormatter
{
    private const string PrivacyRedaction = "Details redacted for privacy.";

    public static ScreenReadingDiagnosticDisplay ToDisplay(this ScreenReadingDiagnosticSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var selectedBackend = Sanitize(snapshot.SelectedBackend);
        var failureBackend = Sanitize(snapshot.FailureBackend);
        var failureMessage = Sanitize(snapshot.FailureMessage);
        var remediation = Sanitize(snapshot.Remediation);
        var policyName = Sanitize(snapshot.PolicyName);

        return new ScreenReadingDiagnosticDisplay(
            HasSelectedBackend: selectedBackend is not null,
            Message: BuildMessage(snapshot, selectedBackend, failureBackend, failureMessage, remediation, policyName),
            IsSupportedSession: snapshot.IsSupportedSession,
            SessionKind: Sanitize(snapshot.SessionKind),
            PolicyName: policyName,
            PolicyOrder: SanitizeValues(snapshot.PolicyOrder),
            SelectedBackend: selectedBackend,
            FailureBackend: failureBackend,
            FailureKind: snapshot.FailureKind?.ToString(),
            FailureMessage: failureMessage,
            Remediation: remediation,
            Backends: snapshot.Backends.Select(ToDisplay).ToArray());
    }

    private static ScreenReadingBackendDiagnosticDisplay ToDisplay(ScreenReadingBackendDiagnostic backend) =>
        new(
            Sanitize(backend.Backend),
            backend.IsAvailable,
            backend.ErrorKind?.ToString(),
            Sanitize(backend.ErrorMessage));

    private static string BuildMessage(
        ScreenReadingDiagnosticSnapshot snapshot,
        string? selectedBackend,
        string? failureBackend,
        string? failureMessage,
        string? remediation,
        string? policyName)
    {
        if (selectedBackend is not null)
        {
            return $"Linux screen reading selects {selectedBackend} backend ({policyName ?? "unknown"} policy).";
        }

        if (!snapshot.IsSupportedSession)
        {
            return "Linux screen reading is unavailable because this session is not a supported Wayland or X11 session.";
        }

        var reason = snapshot.FailureKind == ScreenReadErrorKind.PermissionDenied
            ? $"{failureBackend ?? "selected backend"} reported permission denied"
            : failureMessage ?? "no Linux screen-reading backend is available";

        return remediation is null
            ? $"Linux screen reading is unavailable: {reason}."
            : $"Linux screen reading is unavailable: {reason}. {remediation}";
    }

    private static string[] SanitizeValues(IReadOnlyList<string> values) =>
        values.Select(value => Sanitize(value) ?? string.Empty).ToArray();

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ContainsPrivateContent(value) ? PrivacyRedaction : value;
    }

    private static bool ContainsPrivateContent(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized.Contains("pixel sample", StringComparison.Ordinal)
            || normalized.Contains("raw rgb", StringComparison.Ordinal)
            || normalized.Contains("rgb(", StringComparison.Ordinal)
            || normalized.Contains("frame bytes", StringComparison.Ordinal)
            || normalized.Contains("byte[]", StringComparison.Ordinal)
            || normalized.Contains("crossmacro-kwin-screenshot", StringComparison.Ordinal)
            || normalized.Contains("screen content", StringComparison.Ordinal)
            || normalized.Contains("/tmp/", StringComparison.Ordinal)
            || normalized.Contains("/var/tmp/", StringComparison.Ordinal)
            || normalized.Contains("/run/user/", StringComparison.Ordinal)
            || normalized.Contains("/home/", StringComparison.Ordinal)
            || normalized.Contains(".raw", StringComparison.Ordinal)
            || normalized.Contains(".png", StringComparison.Ordinal)
            || normalized.Contains(".jpg", StringComparison.Ordinal)
            || normalized.Contains(".jpeg", StringComparison.Ordinal)
            || normalized.Contains(".bmp", StringComparison.Ordinal)
            || normalized.Contains(".ppm", StringComparison.Ordinal);
    }
}
