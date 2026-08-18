namespace CrossMacro.Platform.Abstractions.Diagnostics;

public enum LinuxDaemonSocketAccessStatus
{
    Accessible = 0,
    Missing = 1,
    PermissionDenied = 2,
    WrongType = 3,
    ConnectionRefusedOrStale = 4,
    Timeout = 5,
    UnexpectedError = 6,
}
