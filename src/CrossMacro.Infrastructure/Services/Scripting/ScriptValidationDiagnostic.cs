namespace CrossMacro.Infrastructure.Services.Scripting;

public sealed record ScriptValidationDiagnostic(
    string Message,
    int? SourceLineNumber,
    int SourceIndex);
