using System;

namespace CrossMacro.Platform.Linux.Ipc;

public sealed class IpcClientException : Exception
{
    public IpcClientFailureReason Reason { get; }

    public IpcClientException(IpcClientFailureReason reason, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
    }
}
