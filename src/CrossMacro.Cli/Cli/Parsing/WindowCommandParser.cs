using System;

namespace CrossMacro.Cli;

internal static class WindowCommandParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length < 2 || CliParseHelpers.IsHelpToken(args[1]))
        {
            return args.Length >= 2 && CliParseHelpers.IsHelpToken(args[1])
                ? CliParseResult.Help("window")
                : CliParseHelpers.MissingRequiredOperandsWithRemainingOptionsJson(args, 1, "window requires a subcommand.", "crossmacro window active|list|search|wait|focus|close|move|resize|center|maximize|fullscreen|float|workspace ... [--json] [--log-level <level>]");
        }

        return args[1].ToLowerInvariant() switch
        {
            "active" => ParseCommonOnly(args, "window.active", WindowCliAction.Active),
            "list" => ParseCommonOnly(args, "window.list", WindowCliAction.List),
            "search" => ParseSelectorCommand(args, "window.search", WindowCliAction.Search, allowAddress: false, allowClass: true, allowTitle: true, requireTimeout: false),
            "wait" => ParseSelectorCommand(args, "window.wait", WindowCliAction.Wait, allowAddress: false, allowClass: true, allowTitle: true, requireTimeout: true),
            "focus" => ParseSelectorCommand(args, "window.focus", WindowCliAction.Focus, allowAddress: true, allowClass: true, allowTitle: true, requireTimeout: false),
            "close" => ParseSelectorCommand(args, "window.close", WindowCliAction.Close, allowAddress: true, allowClass: false, allowTitle: true, requireTimeout: false),
            "move" => ParseActivePair(args, "window.move", WindowCliAction.Move, "x", "y"),
            "resize" => ParseActivePair(args, "window.resize", WindowCliAction.Resize, "width", "height"),
            "center" => ParseActiveFlag(args, "window.center", WindowCliAction.Center),
            "maximize" => ParseActiveFlag(args, "window.maximize", WindowCliAction.Maximize),
            "fullscreen" => ParseActiveFlag(args, "window.fullscreen", WindowCliAction.Fullscreen),
            "float" => ParseActiveFlag(args, "window.float", WindowCliAction.Float),
            "workspace" => ParseWorkspace(args),
            _ => CliParseResult.Error($"Unknown window subcommand: {args[1]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 2))
        };
    }

    private static CliParseResult ParseCommonOnly(string[] args, string helpTopic, WindowCliAction action)
    {
        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, helpTopic, ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {helpTopic.Replace('.', ' ')}: {args[i]}", jsonOutput);
        }

        return CliParseResult.Success(new WindowCliOptions(action, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseSelectorCommand(string[] args, string helpTopic, WindowCliAction action, bool allowAddress, bool allowClass, bool allowTitle, bool requireTimeout)
    {
        var jsonOutput = false;
        string? logLevel = null;
        WindowSelector? selector = null;
        int? timeoutMs = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, helpTopic, ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (string.Equals(args[i], "--timeout-ms", StringComparison.OrdinalIgnoreCase) && requireTimeout)
            {
                if (!CliParseHelpers.TryReadInt(args, ref i, out var parsedTimeout, out var timeoutError)) return CliParseHelpers.Error(timeoutError, jsonOutput);
                if (parsedTimeout < 0) return CliParseHelpers.Error("--timeout-ms must be >= 0", jsonOutput);
                timeoutMs = parsedTimeout;
                continue;
            }

            if (TryReadSelector(args, ref i, allowAddress, allowClass, allowTitle, ref selector, out var selectorError))
            {
                if (!string.IsNullOrEmpty(selectorError)) return CliParseHelpers.Error(selectorError, jsonOutput);
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {helpTopic.Replace('.', ' ')}: {args[i]}", jsonOutput);
        }

        if (selector is null)
        {
            var allowed = BuildSelectorUsage(allowAddress, allowTitle, allowClass);
            return CliParseHelpers.Error($"{helpTopic.Replace('.', ' ')} requires {allowed}.", jsonOutput);
        }

        return CliParseResult.Success(new WindowCliOptions(action, selector, TimeoutMs: timeoutMs, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseActivePair(string[] args, string helpTopic, WindowCliAction action, string firstName, string secondName)
    {
        var jsonOutput = false;
        string? logLevel = null;
        var active = false;
        int? first = null;
        int? second = null;

        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, helpTopic, ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (string.Equals(args[i], "--active", StringComparison.OrdinalIgnoreCase))
            {
                active = true;
                if (!TryReadOperandInt(args, ref i, firstName, out first, out var firstError)) return CliParseHelpers.Error(firstError, jsonOutput);
                if (!TryReadOperandInt(args, ref i, secondName, out second, out var secondError)) return CliParseHelpers.Error(secondError, jsonOutput);
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {helpTopic.Replace('.', ' ')}: {args[i]}", jsonOutput);
        }

        if (!active || first is null || second is null)
        {
            return CliParseHelpers.Error($"{helpTopic.Replace('.', ' ')} requires --active <{firstName}> <{secondName}>.", jsonOutput);
        }

        if (action == WindowCliAction.Resize && (first <= 0 || second <= 0))
        {
            return CliParseHelpers.Error("window resize dimensions must be positive.", jsonOutput);
        }

        return CliParseResult.Success(action == WindowCliAction.Move
            ? new WindowCliOptions(action, X: first, Y: second, JsonOutput: jsonOutput, LogLevel: logLevel)
            : new WindowCliOptions(action, Width: first, Height: second, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseActiveFlag(string[] args, string helpTopic, WindowCliAction action)
    {
        var jsonOutput = false;
        string? logLevel = null;
        var active = false;
        for (var i = 2; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, helpTopic, ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (string.Equals(args[i], "--active", StringComparison.OrdinalIgnoreCase))
            {
                active = true;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {helpTopic.Replace('.', ' ')}: {args[i]}", jsonOutput);
        }

        return active
            ? CliParseResult.Success(new WindowCliOptions(action, JsonOutput: jsonOutput, LogLevel: logLevel))
            : CliParseHelpers.Error($"{helpTopic.Replace('.', ' ')} requires --active.", jsonOutput);
    }

    private static CliParseResult ParseWorkspace(string[] args)
    {
        if (args.Length < 3 || CliParseHelpers.IsHelpToken(args[2]))
        {
            return args.Length >= 3 && CliParseHelpers.IsHelpToken(args[2])
                ? CliParseResult.Help("window.workspace")
                : CliParseHelpers.Error("window workspace requires get, switch, move-active, or move-window.", CliParseHelpers.HasJsonOption(args, 3));
        }

        return args[2].ToLowerInvariant() switch
        {
            "get" => ParseWorkspaceGet(args),
            "switch" => ParseWorkspaceName(args, "window.workspace.switch", WindowCliAction.WorkspaceSwitch, 3),
            "move-active" => ParseWorkspaceName(args, "window.workspace.move-active", WindowCliAction.WorkspaceMoveActive, 3),
            "move-window" => ParseWorkspaceMoveWindow(args),
            _ => CliParseResult.Error($"Unknown window workspace subcommand: {args[2]}", prefersJsonOutput: CliParseHelpers.HasJsonOption(args, 3))
        };
    }

    private static CliParseResult ParseWorkspaceGet(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        for (var i = 3; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "window.workspace.get", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for window workspace get: {args[i]}", jsonOutput);
        }

        return CliParseResult.Success(new WindowCliOptions(WindowCliAction.WorkspaceGet, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseWorkspaceName(string[] args, string helpTopic, WindowCliAction action, int nameIndex)
    {
        var jsonOutput = false;
        string? logLevel = null;
        string? workspace = null;
        for (var i = nameIndex; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, helpTopic, ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (workspace is null && !args[i].StartsWith("-", StringComparison.Ordinal))
            {
                workspace = args[i];
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for {helpTopic.Replace('.', ' ')}: {args[i]}", jsonOutput);
        }

        return string.IsNullOrWhiteSpace(workspace)
            ? CliParseHelpers.Error($"{helpTopic.Replace('.', ' ')} requires <name>.", jsonOutput)
            : CliParseResult.Success(new WindowCliOptions(action, WorkspaceName: workspace, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static CliParseResult ParseWorkspaceMoveWindow(string[] args)
    {
        var jsonOutput = false;
        string? logLevel = null;
        string? address = null;
        string? workspace = null;
        for (var i = 3; i < args.Length; i++)
        {
            if (CliParseHelpers.TryHandleCommonCliOption(args, ref i, "window.workspace.move-window", ref jsonOutput, ref logLevel, out var common))
            {
                if (common is not null) return common;
                continue;
            }

            if (string.Equals(args[i], "--address", StringComparison.OrdinalIgnoreCase))
            {
                if (!CliParseHelpers.TryReadNonEmptyString(args, ref i, out address, out var addressError)) return CliParseHelpers.Error(addressError, jsonOutput);
                continue;
            }

            if (workspace is null && !args[i].StartsWith("-", StringComparison.Ordinal))
            {
                workspace = args[i];
                continue;
            }

            return CliParseHelpers.ErrorWithRemainingOptionsJson(args, i, $"Unknown option for window workspace move-window: {args[i]}", jsonOutput);
        }

        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(workspace))
        {
            return CliParseHelpers.Error("window workspace move-window requires --address <id> <name>.", jsonOutput);
        }

        return CliParseResult.Success(new WindowCliOptions(WindowCliAction.WorkspaceMoveWindow, new WindowSelector(WindowSelectorKind.Address, address), WorkspaceName: workspace, JsonOutput: jsonOutput, LogLevel: logLevel));
    }

    private static bool TryReadSelector(string[] args, ref int index, bool allowAddress, bool allowClass, bool allowTitle, ref WindowSelector? selector, out string? error)
    {
        error = null;
        var kind = args[index].ToLowerInvariant() switch
        {
            "--address" when allowAddress => WindowSelectorKind.Address,
            "--title" when allowTitle => WindowSelectorKind.Title,
            "--class" when allowClass => WindowSelectorKind.Class,
            _ => (WindowSelectorKind?)null
        };

        if (kind is null)
        {
            return false;
        }

        if (selector is not null)
        {
            error = "Only one window selector may be provided.";
            return true;
        }

        if (!CliParseHelpers.TryReadNonEmptyString(args, ref index, out var value, out error))
        {
            return true;
        }

        selector = new WindowSelector(kind.Value, value);
        error = null;
        return true;
    }

    private static bool TryReadOperandInt(string[] args, ref int index, string name, out int? value, out string error)
    {
        value = null;
        if (index + 1 >= args.Length)
        {
            error = $"Missing <{name}> operand.";
            return false;
        }

        index++;
        if (!int.TryParse(args[index], out var parsed))
        {
            error = $"Invalid integer value for <{name}>: {args[index]}";
            return false;
        }

        value = parsed;
        error = string.Empty;
        return true;
    }

    private static string BuildSelectorUsage(bool address, bool title, bool @class)
    {
        var values = new System.Collections.Generic.List<string>();
        if (address) values.Add("--address <id>");
        if (title) values.Add("--title <text>");
        if (@class) values.Add("--class <text>");
        return string.Join(" or ", values);
    }
}
