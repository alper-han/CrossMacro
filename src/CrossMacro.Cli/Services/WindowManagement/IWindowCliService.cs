
namespace CrossMacro.Cli.Services.WindowManagement;

public interface IWindowCliService
{
    public Task<CliCommandExecutionResult> ExecuteAsync(WindowCliOptions options, CancellationToken cancellationToken);
}
