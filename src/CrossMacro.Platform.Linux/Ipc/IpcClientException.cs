
namespace CrossMacro.Platform.Linux.Ipc;

public sealed class IpcClientException(IpcClientFailureReason reason, string message, Exception? innerException = null) : Exception(message, innerException)
{
    public IpcClientFailureReason Reason { get; } = reason;

    public IpcClientException()
        : this(IpcClientFailureReason.ConnectFailed, "IPC client error occurred.") { /* Empty */ }

    public IpcClientException(string message)
        : this(IpcClientFailureReason.ConnectFailed, message) { /* Empty */ }

    public IpcClientException(string message, Exception innerException)
        : this(IpcClientFailureReason.ConnectFailed, message, innerException) { /* Empty */ }
}
