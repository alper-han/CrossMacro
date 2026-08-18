namespace CrossMacro.Application.Runtime;

public enum RunExecutionStatus
{
    Succeeded,
    InvalidArguments,
    ValidationFailed,
    Cancelled,
    AbsolutePlaybackUnsupported,
    InputInjectionPermissionRequired,
    Failed,
}
