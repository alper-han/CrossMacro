
namespace CrossMacro.Cli.Parsing;

internal static class ProfileCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2 || CliParseHelpers.IsHelpToken(args[1]))
        {
            return args.Length >= 2 && CliParseHelpers.IsHelpToken(args[1])
                ? CliParseResult.Help("profile")
                : CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(
                    args,
                    1,
                    "profile requires list, current, create, switch, rename, or delete.",
                    "crossmacro profile list [--json] [--log-level <level>]",
                    "crossmacro profile current [--json] [--log-level <level>]",
                    "crossmacro profile create <name> [--json] [--log-level <level>]",
                    "crossmacro profile switch <name-or-id> [--json] [--log-level <level>]",
                    "crossmacro profile rename <name-or-id> <new-name> [--json] [--log-level <level>]",
                    "crossmacro profile delete <name-or-id> --force [--json] [--log-level <level>]");
        }

        return args[1].ToLowerInvariant() switch
        {
            "list" => ParseNoOperand(args, ProfileCliAction.List, "profile.list"),
            "current" => ParseNoOperand(args, ProfileCliAction.Current, "profile.current"),
            "create" => ParseOneOperand(args, ProfileCliAction.Create, "profile.create", "profile create requires <name>."),
            "switch" => ParseOneOperand(args, ProfileCliAction.Switch, "profile.switch", "profile switch requires <name-or-id>."),
            "rename" => ParseRename(args),
            "delete" => ParseDelete(args),
            _ => CliParseResult.Error($"Unknown profile subcommand: {args[1]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 2)),
        };
    }

    private static CliParseResult ParseNoOperand(string[] args, ProfileCliAction action, string helpTopic)
    {
        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, helpTopic, ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null)
                {
                    return common;
                }

                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unexpected argument for {helpTopic.Replace('.', ' ')}: {args[i]}", jsonOutput);
        }

        return CliParseResult.Success(new ProfileCliOptions(action, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseOneOperand(string[] args, ProfileCliAction action, string helpTopic, string missingMessage)
    {
        var jsonOutput = false;
        string? logLevel = null;
        string? operand = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, helpTopic, ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null)
                {
                    return common;
                }

                continue;
            }

            if (CliParseHelpers.LooksLikeLongOptionToken(args[i]))
            {
                return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {helpTopic.Replace('.', ' ')}: {args[i]}", jsonOutput);
            }

            if (operand is not null)
            {
                return CliParseHelpers.Error($"{helpTopic.Replace('.', ' ')} accepts one operand.", jsonOutput);
            }

            operand = args[i];
        }

        if (string.IsNullOrWhiteSpace(operand))
        {
            return CliParseHelpers.MissingRequiredOperands(missingMessage, jsonOutput, UsageFor(helpTopic));
        }

        return CliParseResult.Success(new ProfileCliOptions(action, operand, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseRename(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        string? profile = null;
        string? newName = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "profile.rename", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null)
                {
                    return common;
                }

                continue;
            }

            if (CliParseHelpers.LooksLikeLongOptionToken(args[i]))
            {
                return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for profile rename: {args[i]}", jsonOutput);
            }

            if (profile is null)
            {
                profile = args[i];
                continue;
            }

            if (newName is null)
            {
                newName = args[i];
                continue;
            }

            return CliParseHelpers.Error("profile rename accepts <name-or-id> and <new-name>.", jsonOutput);
        }

        if (string.IsNullOrWhiteSpace(profile) || string.IsNullOrWhiteSpace(newName))
        {
            return CliParseHelpers.MissingRequiredOperands(
                "profile rename requires <name-or-id> and <new-name>.",
                jsonOutput,
                UsageFor("profile.rename"));
        }

        return CliParseResult.Success(new ProfileCliOptions(ProfileCliAction.Rename, profile, newName, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseDelete(string[] args)
    {
        var jsonOutput = false;
        var force = false;
        string? logLevel = null;
        string? profile = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "profile.delete", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null)
                {
                    return common;
                }

                continue;
            }

            if (string.Equals(args[i], "--force", StringComparison.OrdinalIgnoreCase))
            {
                force = true;
                continue;
            }

            if (CliParseHelpers.LooksLikeLongOptionToken(args[i]))
            {
                return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for profile delete: {args[i]}", jsonOutput);
            }

            if (profile is not null)
            {
                return CliParseHelpers.Error("profile delete accepts one <name-or-id> operand.", jsonOutput);
            }

            profile = args[i];
        }

        if (string.IsNullOrWhiteSpace(profile))
        {
            return CliParseHelpers.MissingRequiredOperands(
                "profile delete requires <name-or-id>.",
                jsonOutput,
                UsageFor("profile.delete"));
        }

        return CliParseResult.Success(new ProfileCliOptions(ProfileCliAction.Delete, profile, Force: force, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static string UsageFor(string helpTopic)
    {
        return helpTopic switch
        {
            "profile.create" => "crossmacro profile create <name> [--json] [--log-level <level>]",
            "profile.switch" => "crossmacro profile switch <name-or-id> [--json] [--log-level <level>]",
            "profile.rename" => "crossmacro profile rename <name-or-id> <new-name> [--json] [--log-level <level>]",
            "profile.delete" => "crossmacro profile delete <name-or-id> --force [--json] [--log-level <level>]",
            _ => "crossmacro profile [subcommand] [--json] [--log-level <level>]",
        };
    }
}
