namespace CrossMacro.Application.Automation;

public sealed class ShortcutCommands(
    IManageShortcut manageShortcut,
    IShortcutHotkeyNormalizer hotkeyNormalizer) : TaskCommands<ShortcutTask>(manageShortcut), IShortcutCommands
{
    private readonly IManageShortcut _manageShortcut = manageShortcut;
    private readonly IShortcutHotkeyNormalizer _hotkeyNormalizer = hotkeyNormalizer;

    protected override string Kind => "shortcut";

    protected override string CannotEnableReason => "Shortcut task requires a macro path, hotkeys, and valid window rules before it can be enabled.";

    protected override Guid IdOf(ShortcutTask task) => task.Id;

    protected override string NameOf(ShortcutTask task) => task.Name;

    protected override bool CanBeEnabled(ShortcutTask task) => task.CanBeEnabled;

    public Task<TaskCommandResult<ShortcutTask>> RunAsync(string taskId, CancellationToken cancellationToken) =>
        GuardAsync(() => RunCoreAsync(taskId, cancellationToken));

    public Task<TaskCommandResult<ShortcutTask>> ExecuteAsync(ShortcutCommand options, CancellationToken cancellationToken) =>
        GuardAsync(async () =>
        {
            try
            {
                return await ExecuteCoreAsync(options, cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException ex)
            {
                return TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut macros.", [ex.Message]);
            }
        });

    private async Task<TaskCommandResult<ShortcutTask>> RunCoreAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        await _manageShortcut.RunAsync(new TaskRequest(parsed.Task!.Id, ExpectedScopeGeneration: parsed.ScopeGeneration), cancellationToken).ConfigureAwait(false);
        return new(Success: true, "Shortcut task executed.", [], Task: parsed.Task, WasRun: true);
    }

    private async Task<TaskCommandResult<ShortcutTask>> ExecuteCoreAsync(ShortcutCommand options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Action switch
        {
            ShortcutCommandAction.Add => await AddAsync(options, cancellationToken).ConfigureAwait(false),
            ShortcutCommandAction.Edit => await EditAsync(options, cancellationToken).ConfigureAwait(false),
            ShortcutCommandAction.Remove => await RemoveAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false),
            ShortcutCommandAction.Enable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: true, cancellationToken).ConfigureAwait(false),
            ShortcutCommandAction.Disable => await SetEnabledAsync(options.TaskId ?? string.Empty, enabled: false, cancellationToken).ConfigureAwait(false),
            ShortcutCommandAction.Bind => await BindAsync(options, cancellationToken).ConfigureAwait(false),
            _ => TaskCommandResult.Fail<ShortcutTask>("Unknown shortcut action."),
        };
    }

    private async Task<TaskCommandResult<ShortcutTask>> AddAsync(ShortcutCommand options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryNormalizeHotkeys(options.Hotkeys, out var hotkeys, out var hotkeyError))
        {
            return TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut hotkeys.", [hotkeyError!]);
        }

        var scope = await _manageShortcut.ListAsync(cancellationToken).ConfigureAwait(false);
        var task = new ShortcutTask
        {
            Name = options.Name ?? string.Empty,
            MacroFilePath = options.MacroFilePath ?? string.Empty,
        };
        ReplaceHotkeys(task, hotkeys);
        var modeError = ValidateModeOptions(task, options);
        if (modeError is not null)
        {
            return TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut mode.", [modeError]);
        }
        ApplyOptions(task, options);

        if (options.Enabled is not null && !task.TrySetEnabled(options.Enabled.Value))
        {
            return CannotEnable();
        }

        task = await _manageShortcut.AddAsync(task, scope.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ShortcutTask>($"Shortcut task added: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<ShortcutTask>> EditAsync(ShortcutCommand options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        var task = AutomationTaskSnapshots.Copy(parsed.Task!);
        if (!string.IsNullOrWhiteSpace(options.Name))
        {
            task.Name = options.Name;
        }

        if (!string.IsNullOrWhiteSpace(options.MacroFilePath))
        {
            task.MacroFilePath = options.MacroFilePath;
        }

        if (options.Hotkeys is not null)
        {
            if (!TryNormalizeHotkeys(options.Hotkeys, out var hotkeys, out var hotkeyError))
            {
                return TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut hotkeys.", [hotkeyError!]);
            }

            ReplaceHotkeys(task, hotkeys);
        }

        var modeError = ValidateModeOptions(task, options);
        if (modeError is not null)
        {
            return TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut mode.", [modeError]);
        }
        ApplyOptions(task, options);
        if (options.Enabled is not null && !task.TrySetEnabled(options.Enabled.Value))
        {
            return CannotEnable();
        }

        cancellationToken.ThrowIfCancellationRequested();
        task = await _manageShortcut.UpdateAsync(task, parsed.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ShortcutTask>($"Shortcut task updated: {task.Name}.", task);
    }

    private async Task<TaskCommandResult<ShortcutTask>> BindAsync(ShortcutCommand options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null)
        {
            return parsed.Result;
        }

        if (!TryNormalizeHotkeys(options.Hotkeys, out var hotkeys, out var hotkeyError))
        {
            return TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut hotkeys.", [hotkeyError!]);
        }

        var task = AutomationTaskSnapshots.Copy(parsed.Task!);
        ReplaceHotkeys(task, hotkeys);
        cancellationToken.ThrowIfCancellationRequested();
        task = await _manageShortcut.UpdateAsync(task, parsed.ScopeGeneration, cancellationToken).ConfigureAwait(false);
        return TaskCommandResult.Ok<ShortcutTask>($"Shortcut task bound: {task.Name}.", task);
    }

    private bool TryNormalizeHotkeys(
        IReadOnlyList<string>? hotkeys,
        out IReadOnlyList<string> normalized,
        out string? error)
    {
        normalized = [];
        error = null;
        if (hotkeys is null || hotkeys.Count is 0)
        {
            error = "At least one shortcut hotkey is required.";
            return false;
        }

        var values = new List<string>(hotkeys.Count);
        foreach (var hotkey in hotkeys)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
            {
                error = "A shortcut chord is required.";
                return false;
            }

            string? value;
            if (!_hotkeyNormalizer.TryNormalize(hotkey, out value, out error) || string.IsNullOrWhiteSpace(value))
            {
                error ??= "A shortcut chord is required.";
                return false;
            }
            if (values.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            values.Add(value);
        }

        if (values.Count is 0)
        {
            error = "At least one shortcut hotkey is required.";
            return false;
        }

        normalized = values;
        return true;
    }

    private static void ReplaceHotkeys(ShortcutTask task, IReadOnlyList<string> hotkeys)
    {
        task.Hotkeys.Clear();
        foreach (var hotkey in hotkeys)
        {
            task.Hotkeys.Add(hotkey);
        }
    }

    private static string? ValidateModeOptions(ShortcutTask task, ShortcutCommand options)
    {
        if (options.RunOnPressAndRelease is true && (options.RunWhileHeld || options.Loop is true
            || options.RepeatCount is not null || options.RepeatDelayMs is not null
            || options.RepeatDelayMinMs is not null || options.RepeatDelayMaxMs is not null))
        {
            return "Press/release mode cannot be combined with held or loop/repeat options.";
        }
        var pairMode = options.RunOnPressAndRelease ?? (task.RunOnPressAndRelease && !options.RunWhileHeld && options.Loop is not true);
        if (options.ReleaseMacroFilePath is not null && !pairMode)
        {
            return "A release macro requires press/release mode.";
        }
        if (pairMode && (options.RunWhileHeld || options.Loop is true || options.RepeatCount is not null
            || options.RepeatDelayMs is not null || options.RepeatDelayMinMs is not null || options.RepeatDelayMaxMs is not null))
        {
            return "Press/release mode cannot use loop/repeat options.";
        }
        return null;
    }

    private static void ApplyOptions(ShortcutTask task, ShortcutCommand options)
    {
        if (options.RunOnPressAndRelease is not null || options.RunWhileHeld || options.Loop is true)
        {
            task.RunOnPressAndRelease = options.RunOnPressAndRelease is true;
            task.RunWhileHeld = options.RunWhileHeld;
            task.LoopEnabled = options.Loop is true;
            if (!task.RunOnPressAndRelease)
            {
                task.ReleaseMacroFilePath = string.Empty;
            }
        }
        if (options.ReleaseMacroFilePath is not null)
        {
            task.ReleaseMacroFilePath = options.ReleaseMacroFilePath;
        }
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

        if (options.ClearWindowRules)
        {
            task.WindowRules.Clear();
        }

        if (options.WindowRules is not null)
        {
            task.WindowRules.Clear();
            foreach (var rule in options.WindowRules.Where(rule => rule is not null))
            {
                task.WindowRules.Add(new ShortcutWindowRule
                {
                    Field = rule.Field,
                    MatchMode = rule.MatchMode,
                    Value = rule.Value,
                });
            }
        }
    }
}
