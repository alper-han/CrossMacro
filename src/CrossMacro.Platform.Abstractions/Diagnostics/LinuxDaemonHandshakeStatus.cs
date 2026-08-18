namespace CrossMacro.Platform.Abstractions.Diagnostics;

public enum LinuxDaemonHandshakeStatus
{
    Success = 0,
    MissingSocket = 1,
    PermissionDenied = 2,
    WrongSocketType = 3,
    ConnectionRefusedOrStale = 4,
    Timeout = 5,
    ProtocolMismatch = 6,
    HandshakeRejected = 7,
    UnexpectedError = 8,
}
