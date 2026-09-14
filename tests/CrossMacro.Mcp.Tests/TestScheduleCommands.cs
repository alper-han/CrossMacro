namespace CrossMacro.Mcp.Tests;

internal sealed class TestScheduleCliService : IScheduleCliService
{
    public CliCommandExecutionResult ListResult { get; init; } = CliCommandExecutionResult.Ok("Loaded 0 schedule task(s).", new TaskListData<ScheduleTaskData>(0, []));
    public CliCommandExecutionResult ExecuteResult { get; init; } = CliCommandExecutionResult.Ok("Schedule task updated.");
    public int RunCallCount { get; private set; }

    public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken) => Task.FromResult(ListResult);
    public Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken)
    {
        RunCallCount++;
        return Task.FromResult(ExecuteResult);
    }

    public Task<CliCommandExecutionResult> ExecuteAsync(ScheduleCliOptions options, CancellationToken cancellationToken) => Task.FromResult(ExecuteResult);
}
