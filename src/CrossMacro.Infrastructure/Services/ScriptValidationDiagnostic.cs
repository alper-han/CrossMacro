namespace CrossMacro.Infrastructure.Services;

public sealed record class ScriptValidationDiagnostic(
    ScriptValidationCategory Category,
    string Message,
    int? SourceLineNumber,
    int SourceIndex);
