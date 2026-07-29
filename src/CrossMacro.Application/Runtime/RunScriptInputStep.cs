namespace CrossMacro.Application.Runtime;

public sealed record RunScriptInputStep(
    string Step,
    int? SourceLineNumber = null,
    int SourceIndex = 0);
