using System;

namespace CrossMacro.Platform.Linux.Ipc;

public enum IpcClientFailureReason
{
    SocketNotFound = 0,
    ConnectFailed = 1,
    HandshakeFailed = 2,
    ProtocolMismatch = 3,
    Timeout = 4
}

public sealed class IpcClientException : Exception
{
    public IpcClientFailureReason Reason { get; }

    public IpcClientException(IpcClientFailureReason reason, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
    }
}
