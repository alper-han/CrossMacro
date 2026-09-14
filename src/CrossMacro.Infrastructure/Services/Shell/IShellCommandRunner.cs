
namespace CrossMacro.Infrastructure.Services.Shell;

public interface IShellCommandRunner
{
    public Task<ShellCommandResult> RunAsync(
        ShellCommandRequest request,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default);
}
