namespace CrossMacro.Platform.Abstractions.Diagnostics;

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
