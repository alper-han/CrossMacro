namespace CrossMacro.Application.Execution;

internal sealed record RunStepEntry(string Step, int? FileLineNumber, int SourceIndex);
