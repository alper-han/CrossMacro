namespace CrossMacro.Mcp.Tests;

internal sealed class TestScheduleCommands : IScheduleCommands
{
    public TaskCommandResult<ScheduledTask> ListResult { get; init; } = new(Success: true, "Loaded 0 schedule task(s).", [], Tasks: []);
    public TaskCommandResult<ScheduledTask> ExecuteResult { get; init; } = TaskCommandResult.Ok<ScheduledTask>("Schedule task updated.");
    public int ExecuteCallCount { get; private set; }
    public int RunCallCount { get; private set; }

    public Task<TaskCommandResult<ScheduledTask>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ListResult);
    }

    public Task<TaskCommandResult<ScheduledTask>> RunAsync(string taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RunCallCount++;
        return Task.FromResult(ExecuteResult);
    }

    public Task<TaskCommandResult<ScheduledTask>> ExecuteAsync(ScheduleCommand options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecuteCallCount++;
        return Task.FromResult(ExecuteResult);
    }
}
