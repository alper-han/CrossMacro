namespace CrossMacro.Mcp.Tests;

internal sealed class TestTriggerCliService : ITriggerCliService
    {
        public CliCommandExecutionResult ListResult { get; init; } = CliCommandExecutionResult.Ok("Loaded 0 trigger task(s).", new TaskListData<TriggerTaskData>(0, []));
        public CliCommandExecutionResult ExecuteResult { get; init; } = CliCommandExecutionResult.Ok("Trigger task updated.");
        public int ExecuteCallCount { get; private set; }

        public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken) => Task.FromResult(ListResult);
        public Task<CliCommandExecutionResult> ExecuteAsync(TriggerCliOptions options, CancellationToken cancellationToken)
        {
            ExecuteCallCount++;
            return Task.FromResult(ExecuteResult);
        }
    }
