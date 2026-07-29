namespace CrossMacro.Platform.Abstractions.Diagnostics;

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

        var reason = snapshot.FailureKind is ScreenReadErrorKind.PermissionDenied
            ? $"{failureBackend ?? "selected backend"} reported permission denied"
            : failureMessage ?? "no Linux screen-reading backend is available";

        return remediation is null
            ? $"Linux screen reading is unavailable: {reason}."
            : $"Linux screen reading is unavailable: {reason}. {remediation}";
    }

    private static string[] SanitizeValues(IReadOnlyList<string> values) =>
        values.Select(static value => Sanitize(value) ?? string.Empty).ToArray();

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ContainsPrivateContent(value) ? PrivacyRedaction : value;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "S5443:Use a directory that is not publicly writable",
        Justification = "These literals are redaction markers only; this method never opens, writes, or resolves a path.")]
    private static bool ContainsPrivateContent(string value)
    {
        return value.Contains("pixel sample", StringComparison.OrdinalIgnoreCase)
            || value.Contains("raw rgb", StringComparison.OrdinalIgnoreCase)
            || value.Contains("rgb(", StringComparison.OrdinalIgnoreCase)
            || value.Contains("frame bytes", StringComparison.OrdinalIgnoreCase)
            || value.Contains("byte[]", StringComparison.OrdinalIgnoreCase)
            || value.Contains("crossmacro-kwin-screenshot", StringComparison.OrdinalIgnoreCase)
            || value.Contains("screen content", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/tmp/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/var/tmp/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/run/user/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/home/", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".raw", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".png", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".jpg", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".jpeg", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".bmp", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".ppm", StringComparison.OrdinalIgnoreCase);
    }
}
