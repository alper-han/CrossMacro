namespace CrossMacro.Infrastructure.Services.Scripting;

public sealed record ScriptValidationDiagnostic(
    ScriptValidationCategory Category,
    string Message,
    int? SourceLineNumber,
    int SourceIndex);
