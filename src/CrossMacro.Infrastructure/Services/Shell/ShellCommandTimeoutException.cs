
namespace CrossMacro.Infrastructure.Services.Shell;

/// <summary>
/// A shell command exceeded its time limit. The command text is deliberately not kept: commands can carry
/// secrets, and exceptions end up in logs and error reports.
/// </summary>
public sealed class ShellCommandTimeoutException : TimeoutException
{
    public ShellCommandTimeoutException()
        : this(TimeSpan.Zero)
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

    public ShellCommandTimeoutException(TimeSpan timeout, Exception? innerException = null)
        : base($"Shell command timed out after {timeout.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)} ms.", innerException)
    {
        Timeout = timeout;
    }

    public TimeSpan Timeout { get; }
}
