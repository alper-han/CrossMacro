namespace CrossMacro.Platform.Linux.Services.Diagnostics;

internal sealed record ScreenReadingBackendDiagnosticDisplay(
    string? Backend,
    bool IsAvailable,
    string? ErrorKind,
    string? ErrorMessage,
    string? Details = null);
