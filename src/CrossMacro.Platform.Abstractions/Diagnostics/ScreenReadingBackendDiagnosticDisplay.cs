namespace CrossMacro.Platform.Abstractions.Diagnostics;

public sealed record ScreenReadingBackendDiagnosticDisplay(
    string? Backend,
    bool IsAvailable,
    string? ErrorKind,
    string? ErrorMessage,
    string? Details = null);
