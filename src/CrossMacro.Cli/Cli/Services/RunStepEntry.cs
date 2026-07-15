namespace CrossMacro.Cli.Services;

internal sealed record class RunStepEntry(string Step, int? FileLineNumber, int SourceIndex);
