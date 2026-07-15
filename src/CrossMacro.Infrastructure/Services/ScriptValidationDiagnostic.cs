namespace CrossMacro.Infrastructure.Services;

public sealed record ScriptValidationDiagnostic(
    ScriptValidationCategory Category,
    string Message,
    int? SourceLineNumber,
    int SourceIndex);
