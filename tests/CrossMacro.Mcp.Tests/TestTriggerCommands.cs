namespace CrossMacro.Mcp.Tests;

internal sealed class TestTriggerCommands : ITriggerCommands
{
    public TaskCommandResult<TriggerTask> ListResult { get; init; } = new(Success: true, "Loaded 0 trigger task(s).", [], Tasks: []);
    public TaskCommandResult<TriggerTask> ExecuteResult { get; init; } = TaskCommandResult.Ok<TriggerTask>("Trigger task updated.");
    public int ExecuteCallCount { get; private set; }

    public Task<TaskCommandResult<TriggerTask>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ListResult);
    }

    public Task<TaskCommandResult<TriggerTask>> ExecuteAsync(TriggerCommand options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecuteCallCount++;
        return Task.FromResult(ExecuteResult);
    }
}
