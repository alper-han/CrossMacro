namespace CrossMacro.Platform.Abstractions.Diagnostics;

public sealed record class ScreenReadingBackendDiagnosticDisplay(
    string? Backend,
    bool IsAvailable,
    string? ErrorKind,
    string? ErrorMessage);
