using System;
using CrossMacro.Core.Models;

namespace CrossMacro.Cli;

internal static class TextExpansionCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2 || CliParseHelpers.IsHelpToken(args[1]))
        {
            return args.Length >= 2 && CliParseHelpers.IsHelpToken(args[1])
                ? CliParseResult.Help("text-expansion")
                : CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                    args,
                    1,
                    "text-expansion requires list, add, remove, enable, disable, or test.",
                    "crossmacro text-expansion list [--profile <name-or-id>] [--json] [--log-level <level>]",
                    "crossmacro text-expansion add <trigger> <replacement> [--method <method>] [--insertion-mode <mode>] [--direct-typing-method <method>] [--profile <name-or-id>] [--json] [--log-level <level>]",
                    "crossmacro text-expansion remove|enable|disable|test <trigger> [--profile <name-or-id>] [--json] [--log-level <level>]");
        }

        return args[1].ToLowerInvariant() switch
        {
            "list" => ParseList(args),
            "add" => ParseAdd(args),
            "remove" => ParseTriggerCommand(args, TextExpansionCliAction.Remove, "text-expansion.remove"),
            "enable" => ParseTriggerCommand(args, TextExpansionCliAction.Enable, "text-expansion.enable"),
            "disable" => ParseTriggerCommand(args, TextExpansionCliAction.Disable, "text-expansion.disable"),
            "test" => ParseTriggerCommand(args, TextExpansionCliAction.Test, "text-expansion.test"),
            _ => CliParseResult.Error($"Unknown text-expansion subcommand: {args[1]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 2)),
        };
    }

    private static CliParseResult ParseList(string[] args)
    {
        var state = new TextExpansionParseState();
        for (var i = 2; i < args.Length; i++)
        {
            if (TryHandleOption(args, ref i, "text-expansion.list", state, out var optionResult))
            {
                if (optionResult is not null) return optionResult;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unexpected argument for text-expansion list: {args[i]}", state.JsonOutput);
        }

        return CliParseResult.Success(new TextExpansionCliOptions(TextExpansionCliAction.List, ProfileIdentifier: state.ProfileIdentifier, JsonOutput: state.JsonOutput, LogLevel: state.LogLevel));
    }

    private static CliParseResult ParseAdd(string[] args)
    {
        var state = new TextExpansionParseState();
        string? trigger = null;
        string? replacement = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (TryHandleOption(args, ref i, "text-expansion.add", state, out var optionResult))
            {
                if (optionResult is not null) return optionResult;
                continue;
            }

            if (CliParseHelpers.LooksLikeLongOptionToken(args[i]))
            {
                return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for text-expansion add: {args[i]}", state.JsonOutput);
            }

            if (trigger is null)
            {
                trigger = args[i];
                continue;
            }

            if (replacement is null)
            {
                replacement = args[i];
                continue;
            }

            return CliParseHelpers.Error("text-expansion add accepts <trigger> and <replacement>.", state.JsonOutput);
        }

        if (string.IsNullOrWhiteSpace(trigger) || replacement is null)
        {
            return CliParseHelpers.MissingRequiredOperands(
                "text-expansion add requires <trigger> and <replacement>.",
                state.JsonOutput,
                "crossmacro text-expansion add <trigger> <replacement> [--method CtrlV|CtrlShiftV|ShiftInsert] [--insertion-mode Paste|DirectTyping] [--direct-typing-method FastBatch|CompatibleKeyByKey] [--profile <name-or-id>] [--json] [--log-level <level>]");
        }

        return CliParseResult.Success(new TextExpansionCliOptions(
            TextExpansionCliAction.Add,
            trigger,
            replacement,
            state.Method,
            state.InsertionMode,
            state.DirectTypingMethod,
            state.ProfileIdentifier,
            JsonOutput: state.JsonOutput,
            LogLevel: state.LogLevel));
    }

    private static CliParseResult ParseTriggerCommand(string[] args, TextExpansionCliAction action, string helpTopic)
    {
        var state = new TextExpansionParseState();
        string? trigger = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (TryHandleOption(args, ref i, helpTopic, state, out var optionResult))
            {
                if (optionResult is not null) return optionResult;
                continue;
            }

            if (CliParseHelpers.LooksLikeLongOptionToken(args[i]))
            {
                return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {helpTopic.Replace('.', ' ')}: {args[i]}", state.JsonOutput);
            }

            if (trigger is not null)
            {
                return CliParseHelpers.Error($"{helpTopic.Replace('.', ' ')} accepts one <trigger> operand.", state.JsonOutput);
            }

            trigger = args[i];
        }

        if (string.IsNullOrWhiteSpace(trigger))
        {
            return CliParseHelpers.MissingRequiredOperands(
                $"{helpTopic.Replace('.', ' ')} requires <trigger>.",
                state.JsonOutput,
                $"crossmacro {helpTopic.Replace('.', ' ')} <trigger> [--profile <name-or-id>] [--json] [--log-level <level>]");
        }

        return CliParseResult.Success(new TextExpansionCliOptions(action, trigger, ProfileIdentifier: state.ProfileIdentifier, JsonOutput: state.JsonOutput, LogLevel: state.LogLevel));
    }

    private static bool TryHandleOption(string[] args, ref int index, string helpTopic, TextExpansionParseState state, out CliParseResult? result)
    {
        if (CliParseHelpers.TryHandleCommonCliOption(args, ref index, helpTopic, ref state.JsonOutput, ref state.LogLevel, out result))
        {
            return true;
        }

        if (string.Equals(args[index], "--profile", StringComparison.OrdinalIgnoreCase))
        {
            if (!CliParseHelpers.TryReadNonEmptyString(args, ref index, out state.ProfileIdentifier, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            result = null;
            return true;
        }

        if (string.Equals(args[index], "--method", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadEnum(args, ref index, out state.Method, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            result = null;
            return true;
        }

        if (string.Equals(args[index], "--insertion-mode", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadEnum(args, ref index, out state.InsertionMode, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            result = null;
            return true;
        }

        if (string.Equals(args[index], "--direct-typing-method", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryReadEnum(args, ref index, out state.DirectTypingMethod, out var error))
            {
                result = CliParseHelpers.Error(error, state.JsonOutput);
                return true;
            }

            result = null;
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryReadEnum<TEnum>(string[] args, ref int index, out TEnum value, out string error)
        where TEnum : struct, Enum
    {
        if (index + 1 >= args.Length)
        {
            value = default;
            error = $"Missing value after {args[index]}";
            return false;
        }

        index++;
        if (Enum.TryParse(args[index], ignoreCase: true, out value))
        {
            error = string.Empty;
            return true;
        }

        error = $"Invalid value for {args[index - 1]}: {args[index]}. Allowed: {string.Join(", ", Enum.GetNames<TEnum>())}.";
        return false;
    }

    private sealed class TextExpansionParseState
    {
        public bool JsonOutput;
        public string? LogLevel;
        public string? ProfileIdentifier;
        public PasteMethod Method = PasteMethod.CtrlV;
        public TextInsertionMode InsertionMode = TextInsertionMode.Paste;
        public DirectTypingMethod DirectTypingMethod = DirectTypingMethod.FastBatch;
    }
}
