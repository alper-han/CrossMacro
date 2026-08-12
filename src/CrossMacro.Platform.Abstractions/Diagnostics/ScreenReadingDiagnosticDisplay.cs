namespace CrossMacro.Platform.Abstractions.Diagnostics;

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
    IReadOnlyList<ScreenReadingBackendDiagnosticDisplay> Backends,
    string? SelectedBackendDetails = null);
