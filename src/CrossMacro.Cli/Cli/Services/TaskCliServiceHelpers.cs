using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Services;

internal static class TaskCliServiceHelpers
{
    public static async Task<CliCommandExecutionResult> ListTasksAsync<TTask>(
        string taskKind,
        CancellationToken cancellationToken,
        Func<Task> loadAsync,
        Func<IEnumerable<TTask>> getTasks,
        Func<TTask, object> mapTask)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await loadAsync();

        var tasks = getTasks()
            .Select(mapTask)
            .ToArray();

        return CliCommandExecutionResult.Ok(
            $"Loaded {tasks.Length} {taskKind} task(s).",
            data: new
            {
                count = tasks.Length,
                tasks
            });
    }

    public static async Task<CliCommandExecutionResult> RunTaskAsync<TTask>(
        string taskId,
        string taskKindLower,
        string taskKindDisplay,
        CancellationToken cancellationToken,
        Func<Task> loadAsync,
        Func<IEnumerable<TTask>> getTasks,
        Func<TTask, Guid> getTaskId,
        Func<Guid, Task> runTaskAsync,
        Func<TTask, object> mapTaskResult)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Guid.TryParse(taskId, out var parsedTaskId))
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                $"Invalid {taskKindLower} task id format.",
                errors: [$"Task id is not a valid GUID: {taskId}"]);
        }

        await loadAsync();

        var task = getTasks().FirstOrDefault(x => getTaskId(x) == parsedTaskId);
        if (task == null)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                $"{taskKindDisplay} task not found.",
                errors: [$"No {taskKindLower} task found with id: {taskId}"]);
        }

        await runTaskAsync(parsedTaskId);

        return CliCommandExecutionResult.Ok(
            $"{taskKindDisplay} task executed.",
            data: mapTaskResult(task));
    }
}
