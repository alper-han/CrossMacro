
namespace CrossMacro.Cli.Services;

public interface IScheduleCliService
{
    Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken);

    Task<CliCommandExecutionResult> ExecuteAsync(ScheduleCliOptions options, CancellationToken cancellationToken);
}
