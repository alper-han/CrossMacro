namespace CrossMacro.Platform.Abstractions.Setup;

public enum QuickSetupOutcome
{
    Failed,
    Succeeded,
    Cancelled,
    AuthorizationDenied,
    AuthenticationUnavailable,
    PrivilegeUnavailable,
    DeviceAccessUnavailable,
}
