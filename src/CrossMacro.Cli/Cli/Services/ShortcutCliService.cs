using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Cli.Serialization;

namespace CrossMacro.Cli.Services;

public sealed class ShortcutCliService : IShortcutCliService
{
    private readonly IShortcutService _shortcutService;

    public ShortcutCliService(IShortcutService shortcutService)
    {
        _shortcutService = shortcutService;
    }

    public async Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.ListTasksAsync(
            taskKind: "shortcut",
            cancellationToken: cancellationToken,
            loadAsync: () => _shortcutService.LoadAsync(),
            getTasks: () => _shortcutService.Tasks,
            mapTask: x => new ShortcutTaskData(
                x.Id,
                x.Name,
                x.IsEnabled,
                x.HotkeyString,
                x.MacroFilePath,
                x.PlaybackSpeed,
                x.LoopEnabled,
                x.RunWhileHeld,
                x.RepeatCount,
                x.RepeatDelayMs,
                x.LastTriggeredTime,
                x.LastStatus
            ));
    }

    public async Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.RunTaskAsync(
            taskId: taskId,
            taskKindLower: "shortcut",
            taskKindDisplay: "Shortcut",
            cancellationToken: cancellationToken,
            loadAsync: () => _shortcutService.LoadAsync(),
            getTasks: () => _shortcutService.Tasks,
            getTaskId: x => x.Id,
            runTaskAsync: (parsedTaskId, cancellationToken) => _shortcutService.RunTaskAsync(parsedTaskId, cancellationToken),
            mapTaskResult: task => new ShortcutTaskRunData(
                task.Id,
                task.Name,
                task.IsEnabled,
                task.HotkeyString,
                task.MacroFilePath,
                task.LastTriggeredTime,
                task.LastStatus
            ));
    }
}
