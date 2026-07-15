
namespace CrossMacro.Infrastructure.Services;

public interface IShellCommandRunner
{
    Task<ShellCommandResult> RunAsync(
        ShellCommandRequest request,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default);
}
