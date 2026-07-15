namespace CrossMacro.Platform.Linux.Ipc;

public enum IpcClientFailureReason
{
    SocketNotFound = 0,
    ConnectFailed = 1,
    PermissionDenied = 2,
    HandshakeFailed = 3,
    ProtocolMismatch = 4,
    Timeout = 5,
}
