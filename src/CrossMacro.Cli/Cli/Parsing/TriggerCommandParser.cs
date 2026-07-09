using System;
using CrossMacro.Core.Models;

namespace CrossMacro.Cli;

internal static class TriggerCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2)
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                1,
                "trigger requires list, add, edit, remove, enable, or disable.",
                "crossmacro trigger list [--json] [--log-level <level>]",
                "crossmacro trigger add --name <name> --field <field> --match-mode <mode> --value <value> --action <action> [--profile <profile>] [--macro <path>] [--fire-mode <mode>] [--cooldown-ms <ms>] [--debounce-ms <ms>] [--enabled <bool>] [--json] [--log-level <level>]",
                "crossmacro trigger edit <task-id> [--name <name>] [--field <field>] [--match-mode <mode>] [--value <value>] [--action <action>] [--profile <profile>] [--macro <path>] [--fire-mode <mode>] [--cooldown-ms <ms>] [--debounce-ms <ms>] [--enabled <bool>] [--json] [--log-level <level>]",
                "crossmacro trigger remove|enable|disable <task-id> [--json] [--log-level <level>]");
        }

        if (CliParseHelpers.IsHelpToken(args[1]))
        {
            return CliParseResult.Help("trigger");
        }

        return args[1].ToLowerInvariant() switch
        {
            "list" => ParseList(args),
            "add" => ParseAdd(args),
            "edit" => ParseEdit(args),
            "remove" => ParseTaskIdCommand(args, TriggerCliAction.Remove, "trigger.remove"),
            "enable" => ParseTaskIdCommand(args, TriggerCliAction.Enable, "trigger.enable"),
            "disable" => ParseTaskIdCommand(args, TriggerCliAction.Disable, "trigger.disable"),
            _ => CliParseResult.Error($"Unknown trigger subcommand: {args[1]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 2))
        };
    }

    private static CliParseResult ParseList(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "trigger.list", ref jsonOutput, ref logLevel, out var commonResult))
            {
                if (commonResult is not null) return commonResult;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for trigger list: {args[i]}", jsonOutput);
        }

        return CliParseResult.Success(new TriggerListCliOptions(jsonOutput, logLevel));
    }

    private static CliParseResult ParseAdd(string[] args)
    {
        var state = new TriggerParseState();
        for (var i = 2; i < args.Length; i++)
        {
            if (TryHandleOption(args, ref i, "trigger.add", state, out var optionResult))
            {
                if (optionResult is not null) return optionResult;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unexpected argument for trigger add: {args[i]}", state.JsonOutput);
        }

        if (string.IsNullOrWhiteSpace(state.Name) || !state.Field.HasValue || !state.MatchMode.HasValue || !state.TriggerActionVal.HasValue)
        {
            return CliParseHelpers.MissingRequiredOperands(
                "trigger add requires --name <name>, --field <field>, --match-mode <mode>, and --action <action>.",
                state.JsonOutput,
                "crossmacro trigger add --name <name> --field <field> --match-mode <mode> --value <value> --action <action> [--profile <profile>] [--macro <path>] [--fire-mode <mode>] [--cooldown-ms <ms>] [--debounce-ms <ms>] [--enabled <bool>] [--json] [--log-level <level>]");
        }

        return CliParseResult.Success(state.ToOptions(TriggerCliAction.Add));
    }

    private static CliParseResult ParseEdit(string[] args)
    {
        if (args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2]))
        {
            return CliParseResult.Help("trigger.edit");
        }

        if (args.Length < 3 || CliParseHelpers.LooksLikeOptionToken(args[2]))
        {
            return CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                args,
                2,
                "trigger edit requires <task-id>.",
                "crossmacro trigger edit <task-id> [--name <name>] [--field <field>] [--match-mode <mode>] [--value <value>] [--action <action>] [--profile <profile>] [--macro <path>] [--fire-mode <mode>] [--cooldown-ms <ms>] [--debounce-ms <ms>] [--enabled <bool>] [--json] [--log-level <level>]");
        }

        var state = new TriggerParseState { TaskId = args[2] };
        for (var i = 3; i < args.Length; i++)
        {
            if (TryHandleOption(args, ref i, "trigger.edit", state, out var optionResult))
            {
                if (optionResult is not null) return optionResult;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unexpected argument for trigger edit: {args[i]}", state.JsonOutput);
        }

        return CliParseResult.Success(state.ToOptions(TriggerCliAction.Edit));
    }

    private static CliParseResult ParseTaskIdCommand(string[] args, TriggerCliAction action, string helpTopic)
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

        var state = new TriggerParseState { TaskId = args[2] };
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

    private static bool TryHandleOption(string[] args, ref int index, string helpTopic, TriggerParseState state, out CliParseResult? result)
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

        if (string.Equals(token, "--value", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadStringOption(args, ref index, state.JsonOutput, out state.Value, out result);
        }

        if (string.Equals(token, "--profile", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadStringOption(args, ref index, state.JsonOutput, out state.TargetProfileId, out result);
        }

        if (string.Equals(token, "--macro", StringComparison.OrdinalIgnoreCase))
        {
            return TryReadStringOption(args, ref index, state.JsonOutput, out state.MacroFilePath, out result);
        }

        if (string.Equals(token, "--field", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadNonEmptyString(args, ref index, out var fieldStr, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            if (!Enum.TryParse<TriggerField>(fieldStr, true, out var field))
            {
                result = CliParseHelpers.Error($"Invalid trigger field: {fieldStr}. Expected: WindowClass, WindowTitle, Workspace, ProcessName, None", state.JsonOutput);
                return true;
            }

            state.Field = field;
            result = null;
            return true;
        }

        if (string.Equals(token, "--match-mode", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadNonEmptyString(args, ref index, out var modeStr, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            if (!Enum.TryParse<TriggerMatchMode>(modeStr, true, out var mode))
            {
                result = CliParseHelpers.Error($"Invalid match mode: {modeStr}. Expected: Equals, Contains, Regex", state.JsonOutput);
                return true;
            }

            state.MatchMode = mode;
            result = null;
            return true;
        }

        if (string.Equals(token, "--action", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadNonEmptyString(args, ref index, out var actionStr, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            if (!Enum.TryParse<TriggerAction>(actionStr, true, out var action))
            {
                result = CliParseHelpers.Error($"Invalid action: {actionStr}. Expected: SwitchProfile, RunMacro", state.JsonOutput);
                return true;
            }

            state.TriggerActionVal = action;
            result = null;
            return true;
        }

        if (string.Equals(token, "--fire-mode", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadNonEmptyString(args, ref index, out var modeStr, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            if (!Enum.TryParse<TriggerFireMode>(modeStr, true, out var mode))
            {
                result = CliParseHelpers.Error($"Invalid fire mode: {modeStr}. Expected: OnceOnChange, EveryMatch, OnEnter, OnExit", state.JsonOutput);
                return true;
            }

            state.FireMode = mode;
            result = null;
            return true;
        }

        if (string.Equals(token, "--cooldown-ms", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadInt(args, ref index, out var cooldown, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            state.CooldownMs = cooldown;
            result = null;
            return true;
        }

        if (string.Equals(token, "--debounce-ms", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadInt(args, ref index, out var debounce, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            state.DebounceMs = debounce;
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

    private sealed class TriggerParseState
    {
        public bool JsonOutput;
        public string? LogLevel;
        public string? TaskId;
        public string? Name;
        public TriggerField? Field;
        public TriggerMatchMode? MatchMode;
        public string? Value;
        public TriggerAction? TriggerActionVal;
        public string? TargetProfileId;
        public string? MacroFilePath;
        public TriggerFireMode? FireMode;
        public int? CooldownMs;
        public int? DebounceMs;
        public bool? Enabled;

        public TriggerCliOptions ToOptions(TriggerCliAction action)
        {
            return new TriggerCliOptions(
                action,
                TaskId,
                Name,
                Field,
                MatchMode,
                Value,
                TriggerActionVal,
                TargetProfileId,
                MacroFilePath,
                FireMode,
                CooldownMs,
                DebounceMs,
                Enabled,
                JsonOutput,
                LogLevel);
        }
    }
}
