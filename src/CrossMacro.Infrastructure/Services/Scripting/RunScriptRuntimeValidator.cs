namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// Validates the commands of a script whose values are only known at playback: every command is checked for
/// syntax now, and anything that cannot be checked without variables is accepted for the runtime to judge.
/// </summary>
internal sealed class RunScriptRuntimeValidator(RunScriptEventCompiler eventCompiler)
{
    private readonly RunScriptEventCompiler _eventCompiler = eventCompiler ?? throw new ArgumentNullException(nameof(eventCompiler));

    /// <summary>Returns the first problem in <paramref name="nodes"/>, or null when the script is acceptable.</summary>
    public string? Validate(IReadOnlyList<RunScriptNode> nodes, int loopDepth)
    {
        foreach (var node in nodes)
        {
            // break/continue are legal only inside loops; an if block does not open one.
            var blockLoopDepth = node is IfNode ? loopDepth : loopDepth + 1;
            var error = node is CommandNode command
                ? ValidateCommand(command.Source, loopDepth)
                : ValidateBlocks(node, blockLoopDepth);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    public static bool ContainsRuntimeBackedNode(IReadOnlyList<RunScriptNode> nodes) => ContainsCommand(nodes, IsRuntimeBackedCommand);

    public static bool ContainsRuntimeServiceNode(IReadOnlyList<RunScriptNode> nodes) => ContainsCommand(nodes, IsRuntimeServiceCommand);

    private string? ValidateBlocks(RunScriptNode node, int loopDepth)
    {
        foreach (var block in node.Blocks)
        {
            var error = Validate(block, loopDepth);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private string? ValidateCommand(RunScriptStep step, int loopDepth)
    {
        var trimmed = step.Step.Trim();
        var source = RunScriptTreeReader.BuildSourcePrefix(step);

        if (TryValidateServiceStep(trimmed, out var serviceError))
        {
            return serviceError is null ? null : $"{source}: {serviceError}";
        }

        if (RunScriptSyntax.IsBreakCommand(trimmed) || RunScriptSyntax.IsContinueCommand(trimmed))
        {
            return loopDepth is 0 ? $"{source}: {trimmed} can only be used inside repeat/while/for blocks." : null;
        }

        if (IsRuntimeDelayCommand(trimmed) || IsRuntimeVariableCommand(trimmed) || IsRuntimeMoveCommand(trimmed))
        {
            return null;
        }

        var compileResult = _eventCompiler.Compile([step]);
        return compileResult.Success ? null : compileResult.ErrorMessage;
    }

    private static bool TryValidateServiceStep(string step, out string? error)
    {
        error = null;
        if (RunScriptSyntax.IsScreenReadingStep(step))
        {
            _ = RunScriptScreenReadingStepParser.TryValidateStep(step, out error);
        }
        else if (RunScriptSyntax.IsWindowStep(step))
        {
            error = RunScriptWindowExecutor.Validate(step);
        }
        else if (RunScriptSyntax.IsClipboardStep(step))
        {
            error = RunScriptClipboardExecutor.Validate(step);
        }
        else if (RunScriptSyntax.IsShellStep(step))
        {
            error = RunScriptShellExecutor.Validate(step);
        }
        else if (RunScriptSyntax.IsScreenshotStep(step))
        {
            error = RunScriptPlatformSyntax.ValidateScreenshotStep(step);
        }
        else if (RunScriptSyntax.IsMousePositionStep(step))
        {
            error = RunScriptMousePositionExecutor.Validate(step);
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool IsRuntimeVariableCommand(string step) =>
        (RunScriptVariableCommands.TryParseAssignment(step, out _, out var assignmentError) && assignmentError is null)
        || (RunScriptVariableCommands.TryParseUpdate(step, out _, out var updateError) && updateError is null);

    private static bool IsRuntimeDelayCommand(string step)
    {
        if (!step.Contains('$', StringComparison.Ordinal))
        {
            return RunScriptInputSyntax.TryParseDelay(step, out _, out _, out _, out _, out var error) && error is null;
        }

        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], "delay", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts.Length is 2)
        {
            return !parts[1].Contains("..", StringComparison.Ordinal)
                && (IsRuntimeIntegerToken(parts[1])
                    || MacroTiming.TryParseDurationMicroseconds(parts[1], out _));
        }

        if (parts.Length is 3 or 4 && string.Equals(parts[1], "random", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length is 3)
            {
                var range = parts[2].Split("..", 2, StringSplitOptions.TrimEntries);
                return range.Length is 2 && IsRuntimeIntegerToken(range[0]) && IsRuntimeIntegerToken(range[1]);
            }

            return IsRuntimeIntegerToken(parts[2]) && IsRuntimeIntegerToken(parts[3]);
        }

        return false;
    }

    private static bool IsRuntimeIntegerToken(string token)
    {
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var literal))
        {
            return literal >= 0;
        }

        return token.StartsWith('$') && token.Length > 1 && ScriptIdentifierSyntax.IsValidVariableName(token[1..]);
    }

    private static bool IsRuntimeMoveCommand(string step)
    {
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 4 || !string.Equals(parts[0], "move", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!RunScriptSyntax.TryParseMouseMoveMode(parts[1], out _, out _))
        {
            return false;
        }

        return EditorActionScriptTokens.TryParseNumericToken(parts[2], out var xSourceType, out _)
            && EditorActionScriptTokens.TryParseNumericToken(parts[3], out var ySourceType, out _)
            && (xSourceType is ScriptNumericSourceType.VariableReference
                || ySourceType is ScriptNumericSourceType.VariableReference);
    }

    private static bool IsRuntimeServiceCommand(string step)
    {
        var trimmed = step.Trim();
        return RunScriptSyntax.IsScreenReadingStep(trimmed)
            || RunScriptSyntax.IsWindowStep(trimmed)
            || RunScriptSyntax.IsClipboardStep(trimmed)
            || RunScriptSyntax.IsShellStep(trimmed)
            || RunScriptSyntax.IsScreenshotStep(trimmed)
            || RunScriptSyntax.IsMousePositionStep(trimmed);
    }

    private static bool IsRuntimeBackedCommand(string step)
    {
        var trimmed = step.Trim();
        return IsRuntimeServiceCommand(trimmed)
            || IsRuntimeDelayCommand(trimmed)
            || IsRuntimeVariableCommand(trimmed)
            || RunScriptSyntax.IsBreakCommand(trimmed)
            || RunScriptSyntax.IsContinueCommand(trimmed);
    }

    private static bool ContainsCommand(IReadOnlyList<RunScriptNode> nodes, Func<string, bool> predicate) =>
        nodes.Any(node => node is CommandNode command
            ? predicate(command.Source.Step)
            : node.Blocks.Any(block => ContainsCommand(block, predicate)));
}
