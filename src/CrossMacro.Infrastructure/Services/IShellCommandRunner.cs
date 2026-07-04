using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Infrastructure.Services;

public interface IShellCommandRunner
{
    Task<ShellCommandResult> RunAsync(
        ShellCommandRequest request,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default);
}

public sealed record ShellCommandRequest(string Command, string? StandardInput = null, int OutputLimitChars = 65_536);

public sealed record ShellCommandResult(int ExitCode, string StandardOutput, string StandardError);

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
