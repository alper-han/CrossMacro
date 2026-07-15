namespace CrossMacro.Application.Runtime;

public sealed record class RunScriptInputStep(
    string Step,
    int? SourceLineNumber = null,
    int SourceIndex = 0);
