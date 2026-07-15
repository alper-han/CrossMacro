using System;

namespace CrossMacro.Platform.Abstractions.Diagnostics;

public readonly record struct LinuxDaemonHandshakeProbeResult(
    string SocketPath,
    LinuxDaemonHandshakeStatus Status,
    TimeSpan Timeout,
    string? Message = null,
    Exception? Exception = null)
{
    public bool Succeeded => Status is LinuxDaemonHandshakeStatus.Success;
    public bool TimedOut => Status is LinuxDaemonHandshakeStatus.Timeout;

    public static LinuxDaemonHandshakeProbeResult Success(string socketPath, TimeSpan timeout)
    {
        return new(socketPath, LinuxDaemonHandshakeStatus.Success, timeout);
    }

    public static LinuxDaemonHandshakeProbeResult Failed(
        string socketPath,
        TimeSpan timeout,
        LinuxDaemonHandshakeStatus status,
        string? message = null,
        Exception? exception = null)
    {
        if (status is LinuxDaemonHandshakeStatus.Success)
        {
            throw new ArgumentException("Use Success for successful daemon handshakes.", nameof(status));
        }

        return new(socketPath, status, timeout, message, exception);
    }
}
