namespace CrossMacro.Mcp.Tests;

internal sealed class TestShortcutCommands : IShortcutCommands
{
    public TaskCommandResult<ShortcutTask> ListResult { get; init; } = new(Success: true, "Loaded 0 shortcut task(s).", [], Tasks: []);
    public TaskCommandResult<ShortcutTask> ExecuteResult { get; init; } = TaskCommandResult.Ok<ShortcutTask>("Shortcut task updated.");
    public int ExecuteCallCount { get; private set; }
    public int RunCallCount { get; private set; }

    public Task<TaskCommandResult<ShortcutTask>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ListResult);
    }

    public Task<TaskCommandResult<ShortcutTask>> RunAsync(string taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RunCallCount++;
        return Task.FromResult(ExecuteResult);
    }

    public Task<TaskCommandResult<ShortcutTask>> ExecuteAsync(ShortcutCommand options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecuteCallCount++;
        return Task.FromResult(ExecuteResult);
    }
}
