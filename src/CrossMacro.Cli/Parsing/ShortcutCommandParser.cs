
namespace CrossMacro.Cli.Parsing;

internal static class ShortcutCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2)
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                1,
                "shortcut requires list, run, add, edit, remove, enable, disable, or bind.",
                CliUsage.ShortcutLines);
        }

        if (CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help("shortcut");
        }

        return args[1].ToLowerInvariant() switch
        {
            "list" => TaskCommandParser.ParseList(
                args,
                "shortcut",
                (jsonOutput, logLevel) => new ShortcutListCliOptions(jsonOutput, logLevel)),
            "run" => TaskCommandParser.ParseRun(
                args,
                "shortcut",
                CliUsage.ShortcutRun,
                (taskId, jsonOutput, logLevel) => new ShortcutRunCliOptions(taskId, jsonOutput, logLevel)),
            "add" => ParseAdd(args),
            "edit" => ParseEdit(args),
            "remove" => ParseTaskIdCommand(args, ShortcutCliAction.Remove, "shortcut.remove"),
            "enable" => ParseTaskIdCommand(args, ShortcutCliAction.Enable, "shortcut.enable"),
            "disable" => ParseTaskIdCommand(args, ShortcutCliAction.Disable, "shortcut.disable"),
            "bind" => ParseBind(args),
            _ => CliParseResult.Error($"Unknown shortcut subcommand: {args[1]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 2)),
        };
    }

    private static CliParseResult ParseAdd(string[] args)
    {
        var state = new ShortcutParseState();
        for (var i = 2; i < args.Length; i++)
        {
            if (TryHandleOption(args, ref i, "shortcut.add", state, out var optionResult))
            {
                if (optionResult is not null)
                {
                    return optionResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unexpected argument for shortcut add: {args[i]}", state.JsonOutput);
        }

        if (string.IsNullOrWhiteSpace(state.Name) || string.IsNullOrWhiteSpace(state.MacroFilePath) || state.Hotkeys.Count is 0)
        {
            return CliParseHelpers.MissingRequiredOperands(
                "shortcut add requires --name <name>, --macro <path>, and at least one --hotkey <keys>.",
                state.JsonOutput,
                CliUsage.ShortcutAdd);
        }

        if (state.ClearWindowRules)
        {
            return CliParseHelpers.Error("--clear-window-rules is only valid for shortcut edit.", state.JsonOutput);
        }

        return ValidateMode(state, isAdd: true) ?? CliParseResult.Success(state.ToOptions(ShortcutCliAction.Add));
    }

    private static CliParseResult ParseEdit(string[] args)
    {
        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help("shortcut.edit");
        }

        if (args.Length < 3 || CliParseHelpers.LooksLikeOptionToken(args[2]))
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                2,
                "shortcut edit requires <task-id>.",
                CliUsage.ShortcutEdit);
        }

        var state = new ShortcutParseState { TaskId = args[2] };
        for (var i = 3; i < args.Length; i++)
        {
            if (TryHandleOption(args, ref i, "shortcut.edit", state, out var optionResult))
            {
                if (optionResult is not null)
                {
                    return optionResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unexpected argument for shortcut edit: {args[i]}", state.JsonOutput);
        }

        if (state.ClearWindowRules && state.WindowRules.Count > 0)
        {
            return CliParseHelpers.Error("--clear-window-rules cannot be combined with --window-rule.", state.JsonOutput);
        }

        return ValidateMode(state, isAdd: false) ?? CliParseResult.Success(state.ToOptions(ShortcutCliAction.Edit));
    }

    private static CliParseResult ParseBind(string[] args)
    {
        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help("shortcut.bind");
        }

        if (args.Length < 3 || CliParseHelpers.LooksLikeOptionToken(args[2]))
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                2,
                "shortcut bind requires <task-id>.",
                CliUsage.ShortcutBind);
        }

        var state = new ShortcutParseState { TaskId = args[2] };
        for (var i = 3; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "shortcut.bind", ref state.JsonOutput, ref state.LogLevel, out var commonResult))
            {
                if (commonResult is not null)
                {
                    return commonResult;
                }

                continue;
            }

            if (string.Equals(args[i], "--hotkey", StringComparison.OrdinalIgnoreCase))
            {
                var hotkeyResult = CliParseHelpers.ReadStringOption(args, ref i, state.JsonOutput, out var hotkey);
                if (hotkeyResult is null)
                {
                    state.Hotkeys.Add(hotkey!);
                    continue;
                }

                return hotkeyResult;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for shortcut bind: {args[i]}", state.JsonOutput);
        }

        if (state.Hotkeys.Count is 0)
        {
            return CliParseHelpers.MissingRequiredOperands(
                "shortcut bind requires at least one --hotkey <keys>.",
                state.JsonOutput,
                CliUsage.ShortcutBind);
        }

        return CliParseResult.Success(state.ToOptions(ShortcutCliAction.Bind));
    }

    private static CliParseResult ParseTaskIdCommand(string[] args, ShortcutCliAction action, string helpTopic)
    {
        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help(helpTopic);
        }

        if (args.Length < 3 || CliParseHelpers.LooksLikeOptionToken(args[2]))
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                2,
                $"{helpTopic.Replace('.', ' ')} requires <task-id>.",
                CliUsage.ShortcutTask);
        }

        var state = new ShortcutParseState { TaskId = args[2] };
        for (var i = 3; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, helpTopic, ref state.JsonOutput, ref state.LogLevel, out var commonResult))
            {
                if (commonResult is not null)
                {
                    return commonResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {helpTopic.Replace('.', ' ')}: {args[i]}", state.JsonOutput);
        }

        return CliParseResult.Success(state.ToOptions(action));
    }

    private static CliParseResult? ValidateMode(ShortcutParseState state, bool isAdd)
    {
        if (state.ConflictingModes || (state.RunOnPressAndRelease is true && (state.RunWhileHeld || state.Loop is true
            || state.RepeatDelayMs is not null || state.RepeatDelayMinMs is not null || state.RepeatDelayMaxMs is not null))
            || (state.RunOnPressAndRelease is false && (state.RunWhileHeld || state.Loop is true)))
        {
            return CliParseHelpers.Error("Shortcut mode options cannot be combined with incompatible loop/held/normal options.", state.JsonOutput);
        }
        if (isAdd && state.RunOnPressAndRelease is true && state.ReleaseMacroFilePath is null)
        {
            return CliParseHelpers.Error("--press-release requires --release-macro <path>.", state.JsonOutput);
        }
        if (state.ReleaseMacroFilePath is not null && (state.RunOnPressAndRelease is false || (isAdd && state.RunOnPressAndRelease is not true)))
        {
            return CliParseHelpers.Error("--release-macro requires --press-release (or an existing press/release task on edit).", state.JsonOutput);
        }
        return null;
    }

    private static bool TryHandleOption(string[] args, ref int index, string helpTopic, ShortcutParseState state, out CliParseResult? result)
    {
        if (state.TryHandleSharedOption(args, ref index, helpTopic, out result))
        {
            return true;
        }

        var token = args[index];
        if (string.Equals(token, "--hotkey", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadStringOption(args, ref index, state.JsonOutput, out var hotkey);
            if (hotkey is not null)
            {
                state.Hotkeys.Add(hotkey);
            }

            return true;
        }

        if (string.Equals(token, "--speed", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadDoubleOption(args, ref index, state.JsonOutput, out state.Speed);
            return true;
        }

        if (string.Equals(token, "--loop", StringComparison.OrdinalIgnoreCase))
        {
            state.Loop = true;
            result = null;
            return true;
        }

        if (string.Equals(token, "--repeat", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadInt(args, ref index, out var repeat, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            state.RepeatCount = repeat;
            state.Loop = true;
            result = null;
            return true;
        }

        if (string.Equals(token, "--repeat-delay-ms", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadInt(args, ref index, out var delay, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            state.RepeatDelayMs = delay;
            result = null;
            return true;
        }

        if (string.Equals(token, "--random-repeat-delay", StringComparison.OrdinalIgnoreCase))
        {
            result = ReadRandomDelay(args, ref index, state);
            return true;
        }

        if (string.Equals(token, "--press-release", StringComparison.OrdinalIgnoreCase)
            || string.Equals(token, "--normal", StringComparison.OrdinalIgnoreCase))
        {
            var mode = string.Equals(token, "--press-release", StringComparison.OrdinalIgnoreCase);
            state.ConflictingModes |= state.RunOnPressAndRelease is not null && state.RunOnPressAndRelease != mode;
            state.RunOnPressAndRelease = mode;
            result = null;
            return true;
        }

        if (string.Equals(token, "--release-macro", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadStringOption(args, ref index, state.JsonOutput, out state.ReleaseMacroFilePath);
            return true;
        }

        if (string.Equals(token, "--run-while-held", StringComparison.OrdinalIgnoreCase))
        {
            state.RunWhileHeld = true;
            result = null;
            return true;
        }

        if (string.Equals(token, "--window-rule", StringComparison.OrdinalIgnoreCase))
        {
            result = ReadWindowRule(args, ref index, state);
            return true;
        }

        if (string.Equals(token, "--clear-window-rules", StringComparison.OrdinalIgnoreCase))
        {
            state.ClearWindowRules = true;
            result = null;
            return true;
        }

        result = null;
        return false;
    }

    private static CliParseResult? ReadRandomDelay(string[] args, ref int index, ShortcutParseState state)
    {
        if (index + 2 >= args.Length)
        {
            return CliParseHelpers.Error("--random-repeat-delay requires <min-ms> and <max-ms>.", state.JsonOutput);
        }

        index++;
        if (!int.TryParse(args[index], CultureInfo.InvariantCulture, out var min))
        {
            return CliParseHelpers.Error($"Invalid integer value for --random-repeat-delay: {args[index]}", state.JsonOutput);
        }

        index++;
        if (!int.TryParse(args[index], CultureInfo.InvariantCulture, out var max))
        {
            return CliParseHelpers.Error($"Invalid integer value for --random-repeat-delay: {args[index]}", state.JsonOutput);
        }

        state.RepeatDelayMinMs = min;
        state.RepeatDelayMaxMs = max;
        return null;
    }

    private static CliParseResult? ReadWindowRule(string[] args, ref int index, ShortcutParseState state)
    {
        if (index + 3 >= args.Length)
        {
            return CliParseHelpers.Error("--window-rule requires <field> <match-mode> <value>.", state.JsonOutput);
        }

        var fieldText = args[++index];
        var matchModeText = args[++index];
        var value = args[++index];
        if (!CliValueTokens.TryParseDefinedEnum<TriggerField>(fieldText, out var field)
            || field is not (TriggerField.WindowClass or TriggerField.WindowTitle or TriggerField.ProcessName))
        {
            return CliParseHelpers.Error("--window-rule field must be WindowClass, WindowTitle, or ProcessName.", state.JsonOutput);
        }

        if (!CliValueTokens.TryParseDefinedEnum<TriggerMatchMode>(matchModeText, out var matchMode))
        {
            return CliParseHelpers.Error("--window-rule match mode must be Equals, Contains, or Regex.", state.JsonOutput);
        }

        var rule = new ShortcutWindowRule { Field = field, MatchMode = matchMode, Value = value };
        if (!rule.IsValid())
        {
            return CliParseHelpers.Error("--window-rule is invalid.", state.JsonOutput);
        }

        state.WindowRules.Add(rule);
        return null;
    }

    private sealed class ShortcutParseState : TaskParseState
    {
        public List<string> Hotkeys { get; } = [];
        public double? Speed;
        public bool? Loop;
        public int? RepeatCount;
        public int? RepeatDelayMs;
        public int? RepeatDelayMinMs;
        public int? RepeatDelayMaxMs;
        public bool RunWhileHeld;
        public bool? RunOnPressAndRelease;
        public string? ReleaseMacroFilePath;
        public bool ConflictingModes;
        public List<ShortcutWindowRule> WindowRules { get; } = [];
        public bool ClearWindowRules;

        public ShortcutCliOptions ToOptions(ShortcutCliAction action)
        {
            return new ShortcutCliOptions(
                Action: action,
                TaskId: TaskId,
                Name: Name,
                MacroFilePath: MacroFilePath,
                Hotkeys: Hotkeys.Count > 0 ? Hotkeys : null,
                Speed: Speed,
                Loop: Loop,
                RepeatCount: RepeatCount,
                RepeatDelayMs: RepeatDelayMs,
                RepeatDelayMinMs: RepeatDelayMinMs,
                RepeatDelayMaxMs: RepeatDelayMaxMs,
                RunWhileHeld: RunWhileHeld,
                RunOnPressAndRelease: RunOnPressAndRelease,
                ReleaseMacroFilePath: ReleaseMacroFilePath,
                Enabled: Enabled,
                JsonOutput: JsonOutput,
                LogLevel: LogLevel,
                WindowRules: WindowRules.Count > 0 ? WindowRules : null,
                ClearWindowRules: ClearWindowRules);
        }
    }
}
