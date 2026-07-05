using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
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
                x.UseRandomRepeatDelay,
                x.UseRandomRepeatDelay ? x.RepeatDelayMinMs : null,
                x.UseRandomRepeatDelay ? x.RepeatDelayMaxMs : null,
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

    public Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        return options.Action switch
        {
            ShortcutCliAction.Add => AddAsync(options, cancellationToken),
            ShortcutCliAction.Edit => EditAsync(options, cancellationToken),
            ShortcutCliAction.Remove => RemoveAsync(options.TaskId ?? string.Empty, cancellationToken),
            ShortcutCliAction.Enable => SetEnabledAsync(options.TaskId ?? string.Empty, true, cancellationToken),
            ShortcutCliAction.Disable => SetEnabledAsync(options.TaskId ?? string.Empty, false, cancellationToken),
            ShortcutCliAction.Bind => BindAsync(options, cancellationToken),
            _ => Task.FromResult(CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown shortcut action."))
        };
    }

    private async Task<CliCommandExecutionResult> AddAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var task = new ShortcutTask
        {
            Name = options.Name ?? string.Empty,
            MacroFilePath = options.MacroFilePath ?? string.Empty,
            HotkeyString = options.Hotkey ?? string.Empty
        };
        ApplyOptions(task, options);

        if (options.Enabled.HasValue)
        {
            task.IsEnabled = options.Enabled.Value;
        }

        await _shortcutService.LoadAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _shortcutService.AddTask(task);
        cancellationToken.ThrowIfCancellationRequested();
        await _shortcutService.SaveAsync().ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Shortcut task added: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> EditAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        if (!string.IsNullOrWhiteSpace(options.Name)) task.Name = options.Name;
        if (!string.IsNullOrWhiteSpace(options.MacroFilePath)) task.MacroFilePath = options.MacroFilePath;
        if (!string.IsNullOrWhiteSpace(options.Hotkey)) task.HotkeyString = options.Hotkey;
        ApplyOptions(task, options);
        if (options.Enabled.HasValue) task.IsEnabled = options.Enabled.Value;

        cancellationToken.ThrowIfCancellationRequested();
        _shortcutService.UpdateTask(task);
        cancellationToken.ThrowIfCancellationRequested();
        await _shortcutService.SaveAsync().ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Shortcut task updated: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> RemoveAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        cancellationToken.ThrowIfCancellationRequested();
        _shortcutService.RemoveTask(task.Id);
        cancellationToken.ThrowIfCancellationRequested();
        await _shortcutService.SaveAsync().ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Shortcut task removed: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> SetEnabledAsync(string taskId, bool enabled, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        if (enabled && !task.CanBeEnabled)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Shortcut task cannot be enabled.",
                ["Shortcut task requires a macro path and hotkey before it can be enabled."]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _shortcutService.SetTaskEnabled(task.Id, enabled);
        cancellationToken.ThrowIfCancellationRequested();
        await _shortcutService.SaveAsync().ConfigureAwait(false);
        task.IsEnabled = enabled;
        var verb = enabled ? "enabled" : "disabled";
        return CliCommandExecutionResult.Ok($"Shortcut task {verb}: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> BindAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        task.HotkeyString = options.Hotkey ?? string.Empty;
        cancellationToken.ThrowIfCancellationRequested();
        _shortcutService.UpdateTask(task);
        cancellationToken.ThrowIfCancellationRequested();
        await _shortcutService.SaveAsync().ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Shortcut task bound: {task.Name}.", MapTask(task));
    }

    private async Task<(ShortcutTask? Task, CliCommandExecutionResult? Result)> LoadAndFindAsync(string taskId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Guid.TryParse(taskId, out var parsedTaskId))
        {
            return (null, CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Invalid shortcut task id format.",
                [$"Task id is not a valid GUID: {taskId}"]));
        }

        await _shortcutService.LoadAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var task = _shortcutService.Tasks.FirstOrDefault(candidate => candidate.Id == parsedTaskId);
        if (task is null)
        {
            return (null, CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Shortcut task not found.",
                [$"No shortcut task found with id: {taskId}"]));
        }

        return (task, null);
    }

    private static void ApplyOptions(ShortcutTask task, ShortcutCliOptions options)
    {
        if (options.Speed.HasValue) task.PlaybackSpeed = options.Speed.Value;
        if (options.Loop.HasValue) task.LoopEnabled = options.Loop.Value;
        if (options.RepeatCount.HasValue) task.RepeatCount = options.RepeatCount.Value;
        if (options.RepeatDelayMs.HasValue) task.RepeatDelayMs = options.RepeatDelayMs.Value;
        if (options.RepeatDelayMinMs.HasValue && options.RepeatDelayMaxMs.HasValue)
        {
            task.UseRandomRepeatDelay = true;
            task.RepeatDelayMinMs = options.RepeatDelayMinMs.Value;
            task.RepeatDelayMaxMs = options.RepeatDelayMaxMs.Value;
        }

        if (options.RunWhileHeld) task.RunWhileHeld = true;
    }

    private static ShortcutTaskData MapTask(ShortcutTask task)
    {
        return new ShortcutTaskData(
            task.Id,
            task.Name,
            task.IsEnabled,
            task.HotkeyString,
            task.MacroFilePath,
            task.PlaybackSpeed,
            task.LoopEnabled,
            task.RunWhileHeld,
            task.RepeatCount,
            task.RepeatDelayMs,
            task.UseRandomRepeatDelay,
            task.UseRandomRepeatDelay ? task.RepeatDelayMinMs : null,
            task.UseRandomRepeatDelay ? task.RepeatDelayMaxMs : null,
            task.LastTriggeredTime,
            task.LastStatus);
    }
}
