namespace CrossMacro.Cli;

public enum CliExitCode
{
    Success = 0,
    InvalidArguments = 2,
    FileError = 3,
    ValidationError = 4,
    EnvironmentError = 5,
    RuntimeError = 6,
    Cancelled = 130
}
