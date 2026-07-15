namespace CrossMacro.Platform.Abstractions.Diagnostics;

public sealed record ScreenReadingBackendDiagnostic(
    string Backend,
    bool IsAvailable,
    ScreenReadErrorKind? ErrorKind,
    string? ErrorMessage);
