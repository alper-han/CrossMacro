using CrossMacro.Cli.Commands;
using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Tests;

public sealed class McpTaskApplicationBoundaryTests
{
    [Theory]
    [InlineData(typeof(McpTaskTools))]
    [InlineData(typeof(McpScreenTools))]
    public void TypedTools_DoNotRequireCliServiceContracts(Type toolType)
    {
        ArgumentNullException.ThrowIfNull(toolType);
        var dependencies = toolType.GetConstructors().SelectMany(constructor => constructor.GetParameters());
        Assert.DoesNotContain(dependencies, parameter =>
            parameter.ParameterType.Namespace?.StartsWith("CrossMacro.Cli", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task CliAndTypedMcp_UseTheSameApplicationScheduleValidationAndMutation()
    {
        var macroPath = Path.Combine(Path.GetTempPath(), $"crossmacro-boundary-{Guid.NewGuid():N}.macro");
        await File.WriteAllTextAsync(macroPath, "fixture", CancellationToken.None);
        try
        {
            var workflow = new CapturingScheduleWorkflow();
            var commands = new ScheduleCommands(workflow);
            var cli = new ScheduleCommandHandler(commands);
            var shortcut = new TestShortcutCommands();
            var trigger = new TestTriggerCommands();
            var authorization = new McpToolAuthorization(new AllowAllMcpCapabilityPolicy(), new McpPathAuthorizer(new AllowAllMcpPathPolicy()),
                commands, shortcut, trigger, new AutomationTaskAuthorization());
            var tools = new McpTaskTools(commands, shortcut, trigger, authorization);

            var invalidMcp = await tools.AddScheduleAsync("Test", macroPath, weekly: "invalid-day", time: "10:15", cancellationToken: CancellationToken.None);
            var invalidCli = await cli.ExecuteAsync(new ScheduleCliOptions(ScheduleCliAction.Add, Name: "Test", MacroFilePath: macroPath, Weekly: "invalid-day", Time: "10:15"), CancellationToken.None);

            Assert.False(invalidMcp.Outcome.Success);
            Assert.Equal(invalidCli.Message, invalidMcp.Outcome.Message);
            Assert.Null(workflow.Added);

            var valid = await tools.AddScheduleAsync("Test", macroPath, weekly: "mon,fri", time: "10:15", cancellationToken: CancellationToken.None);

            Assert.True(valid.Outcome.Success);
            Assert.NotNull(workflow.Added);
            Assert.Equal(ScheduleDays.Monday | ScheduleDays.Friday, workflow.Added.WeeklyDays);
            Assert.Equal(new TimeSpan(10, 15, 0), workflow.Added.WeeklyTime);
            Assert.Equal(workflow.Added.Id, valid.Task?.Id);
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    private sealed class CapturingScheduleWorkflow : IManageSchedule
    {
        public ScheduledTask? Added { get; private set; }

        public Task<ScheduledTask> AddAsync(ScheduledTask task, CancellationToken cancellationToken = default)
        {
            Added = AutomationTaskSnapshots.Copy(task);
            return Task.FromResult(Added);
        }

        public Task<ScheduledTask> AddAsync(ScheduledTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default) =>
            AddAsync(task, cancellationToken);

        public Task<TaskCollectionResult<ScheduledTask>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TaskCollectionResult<ScheduledTask>(Added is { } task ? [task] : []));
        public Task<ScheduledTask> UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduledTask> UpdateAsync(ScheduledTask task, long expectedScopeGeneration, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduledTask> RemoveAsync(TaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ScheduledTask> SetEnabledAsync(TaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
