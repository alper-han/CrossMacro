namespace CrossMacro.Mcp.Tests;

internal sealed class TestShortcutCliService : IShortcutCliService
    {
        public CliCommandExecutionResult ListResult { get; init; } = CliCommandExecutionResult.Ok("Loaded 0 shortcut task(s).", new TaskListData<ShortcutTaskData>(0, []));
        public CliCommandExecutionResult ExecuteResult { get; init; } = CliCommandExecutionResult.Ok("Shortcut task updated.");

        public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken) => Task.FromResult(ListResult);
        public Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken) => Task.FromResult(ExecuteResult);
        public Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken) => Task.FromResult(ExecuteResult);
    }
