
namespace CrossMacro.Cli.Services.Automation;

public interface IScheduleCliService
{
    public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken);

    public Task<CliCommandExecutionResult> ExecuteAsync(ScheduleCliOptions options, CancellationToken cancellationToken);
}
