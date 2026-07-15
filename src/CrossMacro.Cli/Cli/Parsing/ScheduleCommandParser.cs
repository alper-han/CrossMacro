
namespace CrossMacro.Cli;

internal static class ScheduleCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2)
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                1,
                "schedule requires list, run, add, edit, remove, enable, disable, or next.",
                "crossmacro schedule list [--json] [--log-level <level>]",
                "crossmacro schedule run <task-id> [--json] [--log-level <level>]",
                "crossmacro schedule add --name <name> --macro <path> [--interval <duration>|--at <datetime>|--weekly <days> --time <HH:mm>] [--speed <value>] [--enabled <bool>] [--json] [--log-level <level>]",
                "crossmacro schedule edit <task-id> [--name <name>] [--macro <path>] [--interval <duration>|--at <datetime>|--weekly <days> --time <HH:mm>] [--speed <value>] [--enabled <bool>] [--json] [--log-level <level>]",
                "crossmacro schedule remove|enable|disable|next <task-id> [--json] [--log-level <level>]");
        }

        if (CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help("schedule");
        }

        return args[1].ToLowerInvariant() switch
        {
            "list" => TaskCommandParser.Parse(
                args,
                "schedule",
                (jsonOutput, logLevel) => new ScheduleListCliOptions(jsonOutput, logLevel),
                (taskId, jsonOutput, logLevel) => new ScheduleRunCliOptions(taskId, jsonOutput, logLevel)),
            "run" => TaskCommandParser.Parse(
                args,
                "schedule",
                (jsonOutput, logLevel) => new ScheduleListCliOptions(jsonOutput, logLevel),
                (taskId, jsonOutput, logLevel) => new ScheduleRunCliOptions(taskId, jsonOutput, logLevel)),
            "add" => ParseAdd(args),
            "edit" => ParseEdit(args),
            "remove" => ParseTaskIdCommand(args, ScheduleCliAction.Remove, "schedule.remove"),
            "enable" => ParseTaskIdCommand(args, ScheduleCliAction.Enable, "schedule.enable"),
            "disable" => ParseTaskIdCommand(args, ScheduleCliAction.Disable, "schedule.disable"),
            "next" => ParseTaskIdCommand(args, ScheduleCliAction.Next, "schedule.next"),
            _ => CliParseResult.Error($"Unknown schedule subcommand: {args[1]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 2)),
        };
    }

    private static CliParseResult ParseAdd(string[] args)
    {
        var state = new ScheduleParseState();
        for (var i = 2; i < args.Length; i++)
        {
            if (TryHandleOption(args, ref i, "schedule.add", state, out var optionResult))
            {
                if (optionResult is not null) return optionResult;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unexpected argument for schedule add: {args[i]}", state.JsonOutput);
        }

        if (string.IsNullOrWhiteSpace(state.Name) || string.IsNullOrWhiteSpace(state.MacroFilePath))
        {
            return CliParseHelpers.MissingRequiredOperands(
                "schedule add requires --name <name> and --macro <path>.",
                state.JsonOutput,
                "crossmacro schedule add --name <name> --macro <path> [--interval <duration>|--at <datetime>|--weekly <days> --time <HH:mm>] [--speed <value>] [--enabled <bool>] [--json] [--log-level <level>]");
        }

        var scheduleValidation = ValidateScheduleShape(state);
        if (scheduleValidation is not null) return scheduleValidation;

        return CliParseResult.Success(state.ToOptions(ScheduleCliAction.Add));
    }

    private static CliParseResult ParseEdit(string[] args)
    {
        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help("schedule.edit");
        }

        if (args.Length < 3 || CliParseHelpers.LooksLikeOptionToken(args[2]))
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                2,
                "schedule edit requires <task-id>.",
                "crossmacro schedule edit <task-id> [--name <name>] [--macro <path>] [--interval <duration>|--at <datetime>|--weekly <days> --time <HH:mm>] [--speed <value>] [--enabled <bool>] [--json] [--log-level <level>]");
        }

        var state = new ScheduleParseState { TaskId = args[2] };
        for (var i = 3; i < args.Length; i++)
        {
            if (TryHandleOption(args, ref i, "schedule.edit", state, out var optionResult))
            {
                if (optionResult is not null) return optionResult;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unexpected argument for schedule edit: {args[i]}", state.JsonOutput);
        }

        var scheduleValidation = ValidateScheduleShape(state);
        if (scheduleValidation is not null) return scheduleValidation;

        return CliParseResult.Success(state.ToOptions(ScheduleCliAction.Edit));
    }

    private static CliParseResult ParseTaskIdCommand(string[] args, ScheduleCliAction action, string helpTopic)
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

        var state = new ScheduleParseState { TaskId = args[2] };
        for (var i = 3; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, helpTopic, ref state.JsonOutput, ref state.LogLevel, out var commonResult))
            {
                if (commonResult is not null) return commonResult;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {helpTopic.Replace('.', ' ')}: {args[i]}", state.JsonOutput);
        }

        return CliParseResult.Success(state.ToOptions(action));
    }

    private static bool TryHandleOption(string[] args, ref int index, string helpTopic, ScheduleParseState state, out CliParseResult? result)
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

        if (string.Equals(token, "--interval", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadStringOption(args, ref index, state.JsonOutput, out state.Interval, out result);
        }

        if (string.Equals(token, "--at", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadStringOption(args, ref index, state.JsonOutput, out state.At, out result);
        }

        if (string.Equals(token, "--weekly", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadStringOption(args, ref index, state.JsonOutput, out state.Weekly, out result);
        }

        if (string.Equals(token, "--time", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadStringOption(args, ref index, state.JsonOutput, out state.Time, out result);
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

    private static CliParseResult? ValidateScheduleShape(ScheduleParseState state)
    {
        var scheduleForms = 0;
        if (!string.IsNullOrWhiteSpace(state.Interval)) scheduleForms++;
        if (!string.IsNullOrWhiteSpace(state.At)) scheduleForms++;
        if (!string.IsNullOrWhiteSpace(state.Weekly)) scheduleForms++;

        if (scheduleForms > 1)
        {
            return CliParseHelpers.Error("Use only one schedule form: --interval, --at, or --weekly.", state.JsonOutput);
        }

        if (!string.IsNullOrWhiteSpace(state.Time) && string.IsNullOrWhiteSpace(state.Weekly))
        {
            return CliParseHelpers.Error("--time can only be used with --weekly.", state.JsonOutput);
        }

        return null;
    }

    private sealed class ScheduleParseState
    {
        public bool JsonOutput;
        public string? LogLevel;
        public string? TaskId;
        public string? Name;
        public string? MacroFilePath;
        public string? Interval;
        public string? At;
        public string? Weekly;
        public string? Time;
        public double SpeedValue;
        public double? Speed;
        public bool? Enabled;

        public ScheduleCliOptions ToOptions(ScheduleCliAction action)
        {
            return new ScheduleCliOptions(
                action,
                TaskId,
                Name,
                MacroFilePath,
                Interval,
                At,
                Weekly,
                Time,
                Speed,
                Enabled,
                JsonOutput,
                LogLevel);
        }
    }
}
