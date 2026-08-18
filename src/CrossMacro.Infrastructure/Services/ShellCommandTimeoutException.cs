
namespace CrossMacro.Infrastructure.Services;

public sealed class ShellCommandTimeoutException : TimeoutException
{
    public ShellCommandTimeoutException()
        : this(string.Empty, TimeSpan.Zero)
    {
    }

    public ShellCommandTimeoutException(string? message)
        : base(message)
    {
    }

    public ShellCommandTimeoutException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public ShellCommandTimeoutException(string command, TimeSpan timeout, Exception? innerException = null)
        : base($"Shell command timed out after {timeout.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)} ms.", innerException)
    {
        Command = command;
        Timeout = timeout;
    }

    public string Command { get; } = string.Empty;
    public TimeSpan Timeout { get; }
}
