
namespace CrossMacro.Cli;

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
                "crossmacro shortcut list [--json] [--log-level <level>]",
                "crossmacro shortcut run <task-id> [--json] [--log-level <level>]",
                "crossmacro shortcut add --name <name> --macro <path> --hotkey <keys> [--enabled <bool>] [--json] [--log-level <level>]",
                "crossmacro shortcut edit <task-id> [--name <name>] [--macro <path>] [--hotkey <keys>] [--enabled <bool>] [--json] [--log-level <level>]",
                "crossmacro shortcut remove|enable|disable <task-id> [--json] [--log-level <level>]",
                "crossmacro shortcut bind <task-id> <hotkey> [--json] [--log-level <level>]");
        }

        if (CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help("shortcut");
        }

        return args[1].ToLowerInvariant() switch
        {
            "list" => TaskCommandParser.Parse(
                args,
                "shortcut",
                (jsonOutput, logLevel) => new ShortcutListCliOptions(jsonOutput, logLevel),
                (taskId, jsonOutput, logLevel) => new ShortcutRunCliOptions(taskId, jsonOutput, logLevel)),
            "run" => TaskCommandParser.Parse(
                args,
                "shortcut",
                (jsonOutput, logLevel) => new ShortcutListCliOptions(jsonOutput, logLevel),
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

        if (string.IsNullOrWhiteSpace(state.Name) || string.IsNullOrWhiteSpace(state.MacroFilePath) || string.IsNullOrWhiteSpace(state.Hotkey))
        {
            return CliParseHelpers.MissingRequiredOperands(
                "shortcut add requires --name <name>, --macro <path>, and --hotkey <keys>.",
                state.JsonOutput,
                "crossmacro shortcut add --name <name> --macro <path> --hotkey <keys> [--enabled <bool>] [--json] [--log-level <level>]");
        }

        return CliParseResult.Success(state.ToOptions(ShortcutCliAction.Add));
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
                "crossmacro shortcut edit <task-id> [--name <name>] [--macro <path>] [--hotkey <keys>] [--enabled <bool>] [--json] [--log-level <level>]");
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

        return CliParseResult.Success(state.ToOptions(ShortcutCliAction.Edit));
    }

    private static CliParseResult ParseBind(string[] args)
    {
        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help("shortcut.bind");
        }

        if (args.Length < 4 || CliParseHelpers.LooksLikeOptionToken(args[2]) || CliParseHelpers.LooksLikeOptionToken(args[3]))
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                2,
                "shortcut bind requires <task-id> and <hotkey>.",
                "crossmacro shortcut bind <task-id> <hotkey> [--json] [--log-level <level>]");
        }

        var state = new ShortcutParseState { TaskId = args[2], Hotkey = args[3] };
        for (var i = 4; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "shortcut.bind", ref state.JsonOutput, ref state.LogLevel, out var commonResult))
            {
                if (commonResult is not null)
                {
                    return commonResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for shortcut bind: {args[i]}", state.JsonOutput);
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
                $"crossmacro {helpTopic.Replace('.', ' ')} <task-id> [--json] [--log-level <level>]");
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

    private static bool TryHandleOption(string[] args, ref int index, string helpTopic, ShortcutParseState state, out CliParseResult? result)
    {
        if (CliParseHelpers.TryHandleCommonCliOption(args, ref index, helpTopic, ref state.JsonOutput, ref state.LogLevel, out result))
        {
            return true;
        }

        var token = args[index];
        if (string.Equals(token, "--name", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadStringOption(args, ref index, state.JsonOutput, out state.Name, out result);
        }

        if (string.Equals(token, "--macro", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadStringOption(args, ref index, state.JsonOutput, out state.MacroFilePath, out result);
        }

        if (string.Equals(token, "--hotkey", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadStringOption(args, ref index, state.JsonOutput, out state.Hotkey, out result);
        }

        if (string.Equals(token, "--speed", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadDouble(args, ref index, out state.SpeedValue, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            state.Speed = state.SpeedValue;
            result = null;
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
            if (!TryReadRandomDelay(args, ref index, state.JsonOutput, state, out result))
            {
                return true;
            }

            return true;
        }

        if (string.Equals(token, "--run-while-held", StringComparison.OrdinalIgnoreCase))
        {
            state.RunWhileHeld = true;
            result = null;
            return true;
        }

        if (string.Equals(token, "--enabled", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadBool(args, ref index, out var enabled, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            state.Enabled = enabled;
            result = null;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryReadStringOption(string[] args, ref int index, bool jsonOutput, out string? value, out CliParseResult? result)
    {
        if (!CliParseHelpers.TryReadNonEmptyString(args, ref index, out var parsed, out var error))
        {
            value = null;
            result = CliParseHelpers.Error(error, jsonOutput);
            return true;
        }

        value = parsed;
        result = null;
        return true;
    }

    private static bool TryReadRandomDelay(string[] args, ref int index, bool jsonOutput, ShortcutParseState state, out CliParseResult? result)
    {
        if (index + 2 >= args.Length)
        {
            result = CliParseHelpers.Error("--random-repeat-delay requires <min-ms> and <max-ms>.", jsonOutput);
            return false;
        }

        index++;
        if (!int.TryParse(args[index], out var min))
        {
            result = CliParseHelpers.Error($"Invalid integer value for --random-repeat-delay: {args[index]}", jsonOutput);
            return false;
        }

        index++;
        if (!int.TryParse(args[index], out var max))
        {
            result = CliParseHelpers.Error($"Invalid integer value for --random-repeat-delay: {args[index]}", jsonOutput);
            return false;
        }

        state.RepeatDelayMinMs = min;
        state.RepeatDelayMaxMs = max;
        result = null;
        return true;
    }

    private sealed class ShortcutParseState
    {
        public bool JsonOutput;
        public string? LogLevel;
        public string? TaskId;
        public string? Name;
        public string? MacroFilePath;
        public string? Hotkey;
        public double SpeedValue;
        public double? Speed;
        public bool? Loop;
        public int? RepeatCount;
        public int? RepeatDelayMs;
        public int? RepeatDelayMinMs;
        public int? RepeatDelayMaxMs;
        public bool RunWhileHeld;
        public bool? Enabled;

        public ShortcutCliOptions ToOptions(ShortcutCliAction action)
        {
            return new ShortcutCliOptions(
                action,
                TaskId,
                Name,
                MacroFilePath,
                Hotkey,
                Speed,
                Loop,
                RepeatCount,
                RepeatDelayMs,
                RepeatDelayMinMs,
                RepeatDelayMaxMs,
                RunWhileHeld,
                Enabled,
                JsonOutput,
                LogLevel);
        }
    }
}
