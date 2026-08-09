namespace CrossMacro.Infrastructure.Services;

internal sealed class RunScriptRuntimeValidator(Func<RunScriptStep, RunScriptCompileResult> compileStaticCommand)
{
    private readonly Func<RunScriptStep, RunScriptCompileResult> _compileStaticCommand = compileStaticCommand ?? throw new ArgumentNullException(nameof(compileStaticCommand));

    public RunScriptCompileResult Validate(IReadOnlyList<RunScriptNode> nodes, int loopDepth)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CommandNode command:
                    var commandValidation = ValidateCommand(command.Source, loopDepth);
                    if (!commandValidation.Success)
                    {
                        return commandValidation;
                    }
                    break;
                case RepeatNode repeat:
                    var repeatValidation = Validate(repeat.Body, loopDepth + 1);
                    if (!repeatValidation.Success)
                    {
                        return repeatValidation;
                    }
                    break;
                case WhileNode whileNode:
                    var whileValidation = Validate(whileNode.Body, loopDepth + 1);
                    if (!whileValidation.Success)
                    {
                        return whileValidation;
                    }
                    break;
                case ForNode forNode:
                    var forValidation = Validate(forNode.Body, loopDepth + 1);
                    if (!forValidation.Success)
                    {
                        return forValidation;
                    }
                    break;
                case IfNode ifNode:
                    var trueValidation = Validate(ifNode.TrueBody, loopDepth);
                    if (!trueValidation.Success)
                    {
                        return trueValidation;
                    }

                    if (ifNode.FalseBody is not null)
                    {
                        var falseValidation = Validate(ifNode.FalseBody, loopDepth);
                        if (!falseValidation.Success)
                        {
                            return falseValidation;
                        }
                    }
                    break;
            }
        }

        return RunScriptCompileResult.Ok(new MacroSequence(), initialDelayMicroseconds: 0);
    }

    public static bool ContainsRuntimeBackedNode(IReadOnlyList<RunScriptNode> nodes) => ContainsCommandNode(nodes, IsRuntimeBackedCommand);

    public static bool ContainsRuntimeServiceNode(IReadOnlyList<RunScriptNode> nodes) => ContainsCommandNode(nodes, IsRuntimeServiceCommand);

    private RunScriptCompileResult ValidateCommand(RunScriptStep step, int loopDepth)
    {
        var trimmed = step.Step.Trim();
        var source = BuildSourcePrefix(step);

        if (RunScriptSyntax.IsScreenReadingStep(trimmed))
        {
            return RunScriptScreenReadingStepParser.TryValidateStep(trimmed, out var screenReadingError) && screenReadingError is not null
                ? RunScriptCompileResult.Fail($"{source}: {screenReadingError}")
                : RunScriptCompileResult.Ok(new MacroSequence(), initialDelayMicroseconds: 0);
        }

        if (RunScriptSyntax.IsWindowStep(trimmed))
        {
            var windowError = RunScriptWindowExecutor.Validate(trimmed);
            return windowError is not null ? RunScriptCompileResult.Fail($"{source}: {windowError}") : RunScriptCompileResult.Ok(new MacroSequence(), initialDelayMicroseconds: 0);
        }

        if (RunScriptSyntax.IsClipboardStep(trimmed))
        {
            var clipboardError = RunScriptClipboardExecutor.Validate(trimmed);
            return clipboardError is not null ? RunScriptCompileResult.Fail($"{source}: {clipboardError}") : RunScriptCompileResult.Ok(new MacroSequence(), initialDelayMicroseconds: 0);
        }

        if (RunScriptSyntax.IsShellStep(trimmed))
        {
            var shellError = RunScriptShellExecutor.Validate(trimmed);
            return shellError is not null ? RunScriptCompileResult.Fail($"{source}: {shellError}") : RunScriptCompileResult.Ok(new MacroSequence(), initialDelayMicroseconds: 0);
        }

        if (RunScriptPlatformSyntax.IsScreenshotStep(trimmed))
        {
            var screenshotError = RunScriptPlatformSyntax.ValidateScreenshotStep(trimmed);
            return screenshotError is not null ? RunScriptCompileResult.Fail($"{source}: {screenshotError}") : RunScriptCompileResult.Ok(new MacroSequence(), initialDelayMicroseconds: 0);
        }

        if (RunScriptSyntax.IsBreakCommand(trimmed) || RunScriptSyntax.IsContinueCommand(trimmed))
        {
            return loopDepth is 0
                ? RunScriptCompileResult.Fail($"{source}: {trimmed} can only be used inside repeat/while/for blocks.")
                : RunScriptCompileResult.Ok(new MacroSequence(), initialDelayMicroseconds: 0);
        }

        if (IsRuntimeDelayCommand(trimmed) || IsRuntimeVariableCommand(trimmed))
        {
            return RunScriptCompileResult.Ok(new MacroSequence(), initialDelayMicroseconds: 0);
        }

        if (IsRuntimeMoveCommand(trimmed))
        {
            return RunScriptCompileResult.Ok(new MacroSequence(), initialDelayMicroseconds: 0);
        }

        var compileResult = _compileStaticCommand(step);
        return compileResult.Success ? RunScriptCompileResult.Ok(new MacroSequence(), initialDelayMicroseconds: 0) : RunScriptCompileResult.Fail(compileResult.ErrorMessage);
    }

    private static bool IsRuntimeVariableCommand(string step)
    {
        if (step.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            var payload = step[4..].Trim();
            var equalIndex = payload.IndexOf('=', StringComparison.Ordinal);
            if (equalIndex >= 0)
            {
                return EditorActionScriptTokens.IsValidVariableName(payload[..equalIndex].Trim());
            }

            var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length is 2 && EditorActionScriptTokens.IsValidVariableName(parts[0]);
        }

        if (step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase) || step.StartsWith("dec ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = step[4..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length is 1 or 2 && EditorActionScriptTokens.IsValidVariableName(parts[0]);
        }

        if (step.StartsWith("mul ", StringComparison.OrdinalIgnoreCase) || step.StartsWith("div ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = step[4..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length is 1 or 2 && EditorActionScriptTokens.IsValidVariableName(parts[0]);
        }

        return false;
    }

    private static bool IsRuntimeDelayCommand(string step)
    {
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

        return token.StartsWith('$') && token.Length > 1 && EditorActionScriptTokens.IsValidVariableName(token[1..]);
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
            || RunScriptPlatformSyntax.IsScreenshotStep(trimmed);
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

    private static bool ContainsCommandNode(IReadOnlyList<RunScriptNode> nodes, Func<string, bool> predicate)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CommandNode commandNode when predicate(commandNode.Source.Step):
                    return true;
                case RepeatNode repeatNode when ContainsCommandNode(repeatNode.Body, predicate):
                    return true;
                case IfNode ifNode when ContainsCommandNode(ifNode.TrueBody, predicate)
                    || (ifNode.FalseBody is not null && ContainsCommandNode(ifNode.FalseBody, predicate)):
                    return true;
                case WhileNode whileNode when ContainsCommandNode(whileNode.Body, predicate):
                    return true;
                case ForNode forNode when ContainsCommandNode(forNode.Body, predicate):
                    return true;
            }
        }

        return false;
    }

    private static string BuildSourcePrefix(RunScriptStep entry)
    {
        var index = entry.SourceIndex > 0 ? entry.SourceIndex : 1;
        return entry.SourceLineNumber is not null ? $"Step {index.ToString(CultureInfo.InvariantCulture)} (line {entry.SourceLineNumber.Value.ToString(CultureInfo.InvariantCulture)})"
            : $"Step {index.ToString(CultureInfo.InvariantCulture)}";
    }
}
