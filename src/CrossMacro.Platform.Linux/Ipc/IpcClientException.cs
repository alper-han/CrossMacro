
namespace CrossMacro.Platform.Linux.Ipc;

public sealed class IpcClientException : Exception
{
    public IpcClientFailureReason Reason { get; }

    public IpcClientException()
        : this(IpcClientFailureReason.ConnectFailed, "IPC client error occurred.")
    {
    }

    public IpcClientException(string message)
        : this(IpcClientFailureReason.ConnectFailed, message)
    {
    }

    public IpcClientException(string message, Exception innerException)
        : this(IpcClientFailureReason.ConnectFailed, message, innerException)
    {
    }

    public IpcClientException(IpcClientFailureReason reason, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
    }
}
