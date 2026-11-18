namespace CrossMacro.Application.Automation;

public sealed class ShortcutTaskCommandRules(IShortcutHotkeyNormalizer hotkeyNormalizer) : TaskCommandRules<ShortcutTask, ShortcutCommand>
{
    private readonly IShortcutHotkeyNormalizer _hotkeyNormalizer = hotkeyNormalizer ?? throw new ArgumentNullException(nameof(hotkeyNormalizer));

    public override Func<ShortcutTask, ShortcutCommand, TaskCommandResult<ShortcutTask>?>? Bind => ApplyBind;

    public override TaskCommandResult<ShortcutTask>? ApplyAdd(ShortcutTask task, ShortcutCommand command)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(command);
        if (!TryNormalizeHotkeys(command.Hotkeys, out var hotkeys, out var hotkeyError))
        {
            return TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut hotkeys.", [hotkeyError!]);
        }

        task.Name = command.Name ?? string.Empty;
        task.MacroFilePath = command.MacroFilePath ?? string.Empty;
        ReplaceHotkeys(task, hotkeys);
        return ApplyModeAndOptions(task, command);
    }

    public override TaskCommandResult<ShortcutTask>? ApplyEdit(ShortcutTask task, ShortcutCommand command)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(command);
        if (!string.IsNullOrWhiteSpace(command.Name))
        {
            task.Name = command.Name;
        }

        if (!string.IsNullOrWhiteSpace(command.MacroFilePath))
        {
            task.MacroFilePath = command.MacroFilePath;
        }

        if (command.Hotkeys is not null)
        {
            if (!TryNormalizeHotkeys(command.Hotkeys, out var hotkeys, out var hotkeyError))
            {
                return TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut hotkeys.", [hotkeyError!]);
            }

            ReplaceHotkeys(task, hotkeys);
        }

        return ApplyModeAndOptions(task, command);
    }

    private TaskCommandResult<ShortcutTask>? ApplyBind(ShortcutTask task, ShortcutCommand command)
    {
        if (!TryNormalizeHotkeys(command.Hotkeys, out var hotkeys, out var hotkeyError))
        {
            return TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut hotkeys.", [hotkeyError!]);
        }

        ReplaceHotkeys(task, hotkeys);
        return null;
    }

    private static TaskCommandResult<ShortcutTask>? ApplyModeAndOptions(ShortcutTask task, ShortcutCommand command)
    {
        var modeError = ValidateModeOptions(task, command);
        if (modeError is not null)
        {
            return TaskCommandResult.Fail<ShortcutTask>("Invalid shortcut mode.", [modeError]);
        }

        ApplyOptions(task, command);
        return null;
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
