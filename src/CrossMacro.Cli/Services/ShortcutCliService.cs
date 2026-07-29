
namespace CrossMacro.Cli.Services;

public sealed class ShortcutCliService(IManageShortcut manageShortcut) : IShortcutCliService
{
    private readonly IManageShortcut _manageShortcut = manageShortcut;
    private IReadOnlyList<ShortcutTask> _tasks = [];

    public async Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.ListTasksAsync(
            taskKind: "shortcut",
            loadAsync: async () => _tasks = (await _manageShortcut.ListAsync(cancellationToken).ConfigureAwait(false)).Tasks,
            getTasks: () => _tasks,
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
            ),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.RunTaskAsync(
            taskId: taskId,
            taskKindLower: "shortcut",
            taskKindDisplay: "Shortcut",
            loadAsync: async () => _tasks = (await _manageShortcut.ListAsync(cancellationToken).ConfigureAwait(false)).Tasks,
            getTasks: () => _tasks,
            getTaskId: x => x.Id,
            runTaskAsync: (parsedTaskId, cancellationToken) => _manageShortcut.RunAsync(new TaskRequest(parsedTaskId), cancellationToken),
            mapTaskResult: task => new ShortcutTaskRunData(
                task.Id,
                task.Name,
                task.IsEnabled,
                task.HotkeyString,
                task.MacroFilePath,
                task.LastTriggeredTime,
                task.LastStatus
            ),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Action switch
        {
            ShortcutCliAction.Add => await AddAsync(options, cancellationToken).ConfigureAwait(false),
            ShortcutCliAction.Edit => await EditAsync(options, cancellationToken).ConfigureAwait(false),
            ShortcutCliAction.Remove => await RemoveAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false),
            ShortcutCliAction.Enable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: true, cancellationToken).ConfigureAwait(false),
            ShortcutCliAction.Disable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: false, cancellationToken).ConfigureAwait(false),
            ShortcutCliAction.Bind => await BindAsync(options, cancellationToken).ConfigureAwait(false),
            _ => CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown shortcut action."),
        };
    }

    private async Task<CliCommandExecutionResult> AddAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var task = new ShortcutTask
        {
            Name = options.Name ?? string.Empty,
            MacroFilePath = options.MacroFilePath ?? string.Empty,
            HotkeyString = options.Hotkey ?? string.Empty,
        };
        ApplyOptions(task, options);

        if (options.Enabled is not null)
        {
            task.IsEnabled = options.Enabled.Value;
        }

        _ = await _manageShortcut.AddAsync(task, cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Shortcut task added: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> EditAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        if (!string.IsNullOrWhiteSpace(options.Name))
        {
            task.Name = options.Name;
        }

        if (!string.IsNullOrWhiteSpace(options.MacroFilePath))
        {
            task.MacroFilePath = options.MacroFilePath;
        }

        if (!string.IsNullOrWhiteSpace(options.Hotkey))
        {
            task.HotkeyString = options.Hotkey;
        }

        ApplyOptions(task, options);
        if (options.Enabled is not null)
        {
            task.IsEnabled = options.Enabled.Value;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageShortcut.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Shortcut task updated: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> RemoveAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageShortcut.RemoveAsync(new TaskRequest(task.Id), cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Shortcut task removed: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> SetEnabledAsync(string taskId, bool enabled, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        if (enabled && !task.CanBeEnabled)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Shortcut task cannot be enabled.",
                ["Shortcut task requires a macro path and hotkey before it can be enabled."]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageShortcut.SetEnabledAsync(new TaskRequest(task.Id, enabled), cancellationToken).ConfigureAwait(false);
        task.IsEnabled = enabled;
        var verb = enabled ? "enabled" : "disabled";
        return CliCommandExecutionResult.Ok($"Shortcut task {verb}: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> BindAsync(ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = parsed.Task!;
        task.HotkeyString = options.Hotkey ?? string.Empty;
        cancellationToken.ThrowIfCancellationRequested();
        _ = await _manageShortcut.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Shortcut task bound: {task.Name}.", MapTask(task));
    }

    private async Task<(ShortcutTask? Task, CliCommandExecutionResult? Result)> LoadAndFindAsync(string taskId, CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.FindTaskAsync(
            taskId,
            "shortcut",
            "Shortcut",
            async () => _tasks = (await _manageShortcut.ListAsync(cancellationToken).ConfigureAwait(false)).Tasks,
            task => task.Id,
            cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyOptions(ShortcutTask task, ShortcutCliOptions options)
    {
        if (options.Speed is not null)
        {
            task.PlaybackSpeed = options.Speed.Value;
        }

        if (options.Loop is not null)
        {
            task.LoopEnabled = options.Loop.Value;
        }

        if (options.RepeatCount is not null)
        {
            task.RepeatCount = options.RepeatCount.Value;
        }

        if (options.RepeatDelayMs is not null)
        {
            task.RepeatDelayMs = options.RepeatDelayMs.Value;
        }

        if (options.RepeatDelayMinMs is not null && options.RepeatDelayMaxMs is not null)
        {
            task.UseRandomRepeatDelay = true;
            task.RepeatDelayMinMs = options.RepeatDelayMinMs.Value;
            task.RepeatDelayMaxMs = options.RepeatDelayMaxMs.Value;
        }

        if (options.RunWhileHeld)
        {
            task.RunWhileHeld = true;
        }
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
