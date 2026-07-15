namespace CrossMacro.Platform.Abstractions.Diagnostics;

public sealed record class ScreenReadingBackendDiagnostic(
    string Backend,
    bool IsAvailable,
    ScreenReadErrorKind? ErrorKind,
    string? ErrorMessage);
