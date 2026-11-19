
namespace CrossMacro.Cli.Parsing;

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
                CliUsage.ScheduleLines);
        }

        if (CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help("schedule");
        }

        return args[1].ToLowerInvariant() switch
        {
            "list" => TaskCommandParser.ParseList(
                args,
                "schedule",
                (jsonOutput, logLevel) => new ScheduleListOptions(jsonOutput, logLevel)),
            "run" => TaskCommandParser.ParseRun(
                args,
                "schedule",
                CliUsage.ScheduleRun,
                (taskId, jsonOutput, logLevel) => new ScheduleRunOptions(taskId, jsonOutput, logLevel)),
            "add" => ParseAdd(args),
            "edit" => ParseEdit(args),
            "remove" => ParseTaskIdCommand(args, ScheduleAction.Remove, "schedule.remove"),
            "enable" => ParseTaskIdCommand(args, ScheduleAction.Enable, "schedule.enable"),
            "disable" => ParseTaskIdCommand(args, ScheduleAction.Disable, "schedule.disable"),
            "next" => ParseTaskIdCommand(args, ScheduleAction.Next, "schedule.next"),
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
                if (optionResult is not null)
                {
                    return optionResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unexpected argument for schedule add: {args[i]}", state.JsonOutput);
        }

        if (string.IsNullOrWhiteSpace(state.Name) || string.IsNullOrWhiteSpace(state.MacroFilePath))
        {
            return CliParseHelpers.MissingRequiredOperands(
                "schedule add requires --name <name> and --macro <path>.",
                state.JsonOutput,
                CliUsage.ScheduleAdd);
        }

        var scheduleValidation = ValidateScheduleShape(state);
        if (scheduleValidation is not null)
        {
            return scheduleValidation;
        }

        return CliParseResult.Success(state.ToOptions(ScheduleAction.Add));
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
                CliUsage.ScheduleEdit);
        }

        var state = new ScheduleParseState { TaskId = args[2] };
        for (var i = 3; i < args.Length; i++)
        {
            if (TryHandleOption(args, ref i, "schedule.edit", state, out var optionResult))
            {
                if (optionResult is not null)
                {
                    return optionResult;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unexpected argument for schedule edit: {args[i]}", state.JsonOutput);
        }

        var scheduleValidation = ValidateScheduleShape(state);
        if (scheduleValidation is not null)
        {
            return scheduleValidation;
        }

        return CliParseResult.Success(state.ToOptions(ScheduleAction.Edit));
    }

    private static CliParseResult ParseTaskIdCommand(string[] args, ScheduleAction action, string helpTopic)
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
                CliUsage.ScheduleTask);
        }

        var state = new ScheduleParseState { TaskId = args[2] };
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

    private static bool TryHandleOption(string[] args, ref int index, string helpTopic, ScheduleParseState state, out CliParseResult? result)
    {
        if (state.TryHandleSharedOption(args, ref index, helpTopic, out result))
        {
            return true;
        }

        var token = args[index];
        if (string.Equals(token, "--interval", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadStringOption(args, ref index, state.JsonOutput, out state.Interval);
            return true;
        }

        if (string.Equals(token, "--at", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadStringOption(args, ref index, state.JsonOutput, out state.At);
            return true;
        }

        if (string.Equals(token, "--weekly", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadStringOption(args, ref index, state.JsonOutput, out state.Weekly);
            return true;
        }

        if (string.Equals(token, "--time", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadStringOption(args, ref index, state.JsonOutput, out state.Time);
            return true;
        }

        if (string.Equals(token, "--speed", StringComparison.OrdinalIgnoreCase))
        {
            result = CliParseHelpers.ReadDoubleOption(args, ref index, state.JsonOutput, out state.Speed);
            return true;
        }

        result = null;
        return false;
    }

    private static CliParseResult? ValidateScheduleShape(ScheduleParseState state)
    {
        var scheduleForms = 0;
        if (!string.IsNullOrWhiteSpace(state.Interval))
        {
            scheduleForms++;
        }

        if (!string.IsNullOrWhiteSpace(state.At))
        {
            scheduleForms++;
        }

        if (!string.IsNullOrWhiteSpace(state.Weekly))
        {
            scheduleForms++;
        }

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

    private sealed class ScheduleParseState : TaskParseState
    {
        public string? Interval;
        public string? At;
        public string? Weekly;
        public string? Time;
        public double? Speed;

        public ScheduleOptions ToOptions(ScheduleAction action)
        {
            return new ScheduleOptions(
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
