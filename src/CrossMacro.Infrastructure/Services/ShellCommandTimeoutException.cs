using System;

namespace CrossMacro.Infrastructure.Services;

public sealed class ShellCommandTimeoutException : TimeoutException
{
    public ShellCommandTimeoutException(string command, TimeSpan timeout, Exception? innerException = null)
        : base($"Shell command timed out after {timeout.TotalMilliseconds:0} ms.", innerException)
    {
        Command = command;
        Timeout = timeout;
    }

    public string Command { get; }
    public TimeSpan Timeout { get; }
}
