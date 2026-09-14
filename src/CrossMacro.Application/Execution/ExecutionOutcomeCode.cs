namespace CrossMacro.Application.Execution;

/// <summary>Host-independent execution outcome categories. Numeric values preserve existing host response codes.</summary>
public enum ExecutionOutcomeCode
{
    Success = 0,
    InvalidArguments = 2,
    FileError = 3,
    ValidationError = 4,
    EnvironmentError = 5,
    RuntimeError = 6,
    Cancelled = 130,
}
