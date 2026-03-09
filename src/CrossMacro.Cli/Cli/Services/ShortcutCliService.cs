using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;

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
            mapTask: x => new
            {
                id = x.Id,
                name = x.Name,
                enabled = x.IsEnabled,
                hotkey = x.HotkeyString,
                macroFilePath = x.MacroFilePath,
                playbackSpeed = x.PlaybackSpeed,
                loopEnabled = x.LoopEnabled,
                runWhileHeld = x.RunWhileHeld,
                repeatCount = x.RepeatCount,
                repeatDelayMs = x.RepeatDelayMs,
                lastTriggeredTime = x.LastTriggeredTime,
                lastStatus = x.LastStatus
            });
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
            mapTaskResult: task => new
            {
                id = task.Id,
                name = task.Name,
                enabled = task.IsEnabled,
                hotkey = task.HotkeyString,
                macroFilePath = task.MacroFilePath,
                lastTriggeredTime = task.LastTriggeredTime,
                lastStatus = task.LastStatus
            });
    }
}
