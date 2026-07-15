
namespace CrossMacro.Cli.Services;

public interface IShortcutCliService
{
    public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken);
}
