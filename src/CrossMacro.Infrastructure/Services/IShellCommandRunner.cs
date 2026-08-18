
namespace CrossMacro.Infrastructure.Services;

public interface IShellCommandRunner
{
    public Task<ShellCommandResult> RunAsync(
        ShellCommandRequest request,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default);
}
