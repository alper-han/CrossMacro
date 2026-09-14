namespace CrossMacro.Cli;

public static class ExecutionOutcomeMapping
{
    public static CliExitCode ToCliExitCode(this ExecutionOutcomeCode outcome) => outcome switch
    {
        ExecutionOutcomeCode.Success => CliExitCode.Success,
        ExecutionOutcomeCode.InvalidArguments => CliExitCode.InvalidArguments,
        ExecutionOutcomeCode.FileError => CliExitCode.FileError,
        ExecutionOutcomeCode.ValidationError => CliExitCode.ValidationError,
        ExecutionOutcomeCode.EnvironmentError => CliExitCode.EnvironmentError,
        ExecutionOutcomeCode.RuntimeError => CliExitCode.RuntimeError,
        ExecutionOutcomeCode.Cancelled => CliExitCode.Cancelled,
        _ => CliExitCode.RuntimeError,
    };
}
