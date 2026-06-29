using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptRuntimeExecutor
{
    private enum LoopControlSignal
    {
        None,
        Break,
        Continue
    }

    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly IPlaybackTimingService _timingService;
    private readonly IPlaybackPauseToken _pauseToken;
    private readonly IDictionary<string, string> _runtimeVariables;
    private readonly RunScriptScreenReadExecutor _screenReadExecutor;
    private readonly RunScriptWindowExecutor _windowExecutor;

    public RunScriptRuntimeExecutor(
        IKeyCodeMapper keyCodeMapper,
        IPlaybackTimingService timingService,
        IPlaybackPauseToken pauseToken,
        IDictionary<string, string> runtimeVariables,
        RunScriptScreenReadExecutor screenReadExecutor,
        RunScriptWindowExecutor windowExecutor)
    {
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
        _timingService = timingService ?? throw new ArgumentNullException(nameof(timingService));
        _pauseToken = pauseToken ?? throw new ArgumentNullException(nameof(pauseToken));
        _runtimeVariables = runtimeVariables ?? throw new ArgumentNullException(nameof(runtimeVariables));
        _screenReadExecutor = screenReadExecutor ?? throw new ArgumentNullException(nameof(screenReadExecutor));
        _windowExecutor = windowExecutor ?? throw new ArgumentNullException(nameof(windowExecutor));
    }

    public async Task ExecuteAsync(RunScriptRuntimeExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var steps = request.ScriptSteps
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .Select(step => step.Trim())
            .ToList();

        await ExecuteRangeAsync(steps, 0, steps.Count, request, cancellationToken);
    }

    private async Task<LoopControlSignal> ExecuteRangeAsync(
        IReadOnlyList<string> steps,
        int start,
        int end,
        RunScriptRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var index = start;
        while (index < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = steps[index];

            if (RunScriptSyntax.IsBlockEndToken(step) || RunScriptSyntax.IsElseHeader(step))
            {
                return LoopControlSignal.None;
            }

            if (TryParseBlockHeader(step, "if", out var ifCondition))
            {
                var trueStart = index + 1;
                var trueEnd = FindBlockEnd(steps, trueStart, end);
                var afterIf = trueEnd + 1;
                var falseStart = -1;
                var falseEnd = -1;
                if (afterIf < end && RunScriptSyntax.IsElseHeader(steps[afterIf]))
                {
                    falseStart = afterIf + 1;
                    falseEnd = FindBlockEnd(steps, falseStart, end);
                    afterIf = falseEnd + 1;
                }

                var signal = EvaluateCondition(ifCondition)
                    ? await ExecuteRangeAsync(steps, trueStart, trueEnd, request, cancellationToken)
                    : falseStart >= 0
                        ? await ExecuteRangeAsync(steps, falseStart, falseEnd, request, cancellationToken)
                        : LoopControlSignal.None;
                if (signal != LoopControlSignal.None)
                {
                    return signal;
                }

                index = afterIf;
                continue;
            }

            if (TryParseBlockHeader(step, "while", out var whileCondition))
            {
                var bodyStart = index + 1;
                var bodyEnd = FindBlockEnd(steps, bodyStart, end);
                var iterations = 0;
                while (EvaluateCondition(whileCondition))
                {
                    if (++iterations > 100_000)
                    {
                        throw new InvalidOperationException("Runtime while loop iteration limit exceeded (100000). Check loop exit condition.");
                    }

                    var signal = await ExecuteRangeAsync(steps, bodyStart, bodyEnd, request, cancellationToken);
                    if (signal == LoopControlSignal.Break)
                    {
                        break;
                    }

                    if (signal == LoopControlSignal.Continue)
                    {
                        continue;
                    }
                }

                index = bodyEnd + 1;
                continue;
            }

            if (TryParseRepeatHeader(step, out var repeatCount))
            {
                var bodyStart = index + 1;
                var bodyEnd = FindBlockEnd(steps, bodyStart, end);
                for (var i = 0; i < repeatCount; i++)
                {
                    var signal = await ExecuteRangeAsync(steps, bodyStart, bodyEnd, request, cancellationToken);
                    if (signal == LoopControlSignal.Break)
                    {
                        break;
                    }

                    if (signal == LoopControlSignal.Continue)
                    {
                        continue;
                    }
                }

                index = bodyEnd + 1;
                continue;
            }

            if (TryParseForHeader(step, out var forVariableName, out var forStart, out var forEnd, out var forStep))
            {
                var bodyStart = index + 1;
                var bodyEnd = FindBlockEnd(steps, bodyStart, end);
                if (forStep == 0)
                {
                    throw new InvalidOperationException("For step cannot be 0.");
                }

                for (var i = forStart; forStep > 0 ? i <= forEnd : i >= forEnd; i += forStep)
                {
                    _runtimeVariables[forVariableName] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var signal = await ExecuteRangeAsync(steps, bodyStart, bodyEnd, request, cancellationToken);
                    if (signal == LoopControlSignal.Break)
                    {
                        break;
                    }

                    if (signal == LoopControlSignal.Continue)
                    {
                        continue;
                    }
                }

                index = bodyEnd + 1;
                continue;
            }

            if (string.Equals(step, RunScriptSyntax.BreakCommand, StringComparison.OrdinalIgnoreCase))
            {
                return LoopControlSignal.Break;
            }

            if (string.Equals(step, RunScriptSyntax.ContinueCommand, StringComparison.OrdinalIgnoreCase))
            {
                return LoopControlSignal.Continue;
            }

            await ExecuteCommandAsync(step, index + 1, request, cancellationToken);
            index++;
        }

        return LoopControlSignal.None;
    }

    private Task ExecuteCommandAsync(
        string step,
        int stepNumber,
        RunScriptRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteCommandCoreAsync(step, stepNumber, request, cancellationToken);
    }

    private async Task ExecuteCommandCoreAsync(
        string step,
        int stepNumber,
        RunScriptRuntimeExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (RunScriptScreenReadExecutor.IsScreenReadingStep(step))
        {
            await _screenReadExecutor.ExecuteStepAsync(step, stepNumber, _runtimeVariables, cancellationToken);
            return;
        }

        if (RunScriptWindowExecutor.IsWindowStep(step))
        {
            await _windowExecutor.ExecuteStepAsync(step, stepNumber, _runtimeVariables, cancellationToken);
            return;
        }

        if (TryParseDelayCommand(step, out var delayMs, request))
        {
            if (delayMs > 0)
            {
                await _timingService.WaitAsync((int)(delayMs / request.SpeedMultiplier), _pauseToken, cancellationToken);
            }

            return;
        }

        if (TryApplyVariableCommand(step))
        {
            return;
        }

        var resolvedStep = ResolveVariables(step);
        var compiler = new RunScriptCompiler(_keyCodeMapper);
        var compileResult = compiler.Compile([new RunScriptStep(resolvedStep)]);
        if (!compileResult.Success || compileResult.Sequence == null)
        {
            throw new InvalidOperationException($"Step {stepNumber}: {compileResult.ErrorMessage}");
        }

        foreach (var ev in compileResult.Sequence.Events)
        {
            await request.ExecuteEventAsync(ev, cancellationToken);
        }
    }

    private bool TryApplyVariableCommand(string step)
    {
        if (step.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            var payload = step[4..].Trim();
            var equalIndex = payload.IndexOf('=');
            string variableName;
            string value;
            if (equalIndex >= 0)
            {
                variableName = payload[..equalIndex].Trim();
                value = payload[(equalIndex + 1)..].Trim();
            }
            else
            {
                var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                {
                    throw new InvalidOperationException("Invalid set syntax. Expected: set <name> <value> or set <name>=<value>.");
                }

                variableName = parts[0];
                value = parts[1];
            }

            EnsureValidVariableName(variableName);
            _runtimeVariables[variableName] = ResolveVariables(value);
            return true;
        }

        if (step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase)
            || step.StartsWith("dec ", StringComparison.OrdinalIgnoreCase))
        {
            var sign = step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
            var payload = step[4..].Trim();
            var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is < 1 or > 2)
            {
                throw new InvalidOperationException("Invalid inc/dec syntax. Expected: inc <name> [amount] or dec <name> [amount].");
            }

            var variableName = parts[0];
            EnsureValidVariableName(variableName);
            if (!_runtimeVariables.TryGetValue(variableName, out var existingValue)
                || !int.TryParse(existingValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var existingInt))
            {
                throw new InvalidOperationException($"Variable '{variableName}' must exist and be an integer for inc/dec.");
            }

            var amountToken = parts.Length == 2 ? ResolveVariables(parts[1]) : "1";
            if (!int.TryParse(amountToken, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var amount))
            {
                throw new InvalidOperationException($"Invalid inc/dec amount '{amountToken}'. Expected integer.");
            }

            _runtimeVariables[variableName] = (existingInt + sign * amount).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private bool TryParseDelayCommand(string step, out int delayMs, RunScriptRuntimeExecutionRequest request)
    {
        delayMs = 0;
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], "delay", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parts.Length == 2 && int.TryParse(ResolveVariables(parts[1]), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var fixedDelay))
        {
            delayMs = Math.Max(0, fixedDelay);
            return true;
        }

        if (parts.Length is 3 or 4 && string.Equals(parts[1], "random", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 3)
            {
                var range = ResolveVariables(parts[2]).Split("..", 2, StringSplitOptions.TrimEntries);
                if (range.Length == 2
                    && int.TryParse(range[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var rangeMin)
                    && int.TryParse(range[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var rangeMax))
                {
                    delayMs = request.ResolveDelayMs(0, true, rangeMin, rangeMax);
                    return true;
                }
            }

            if (parts.Length == 4
                && int.TryParse(ResolveVariables(parts[2]), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var min)
                && int.TryParse(ResolveVariables(parts[3]), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var max))
            {
                delayMs = request.ResolveDelayMs(0, true, min, max);
                return true;
            }
        }

        return false;
    }

    private static bool TryParseBlockHeader(string step, string keyword, out string condition)
    {
        condition = string.Empty;
        var prefix = keyword + " ";
        if (!step.EndsWith("{", StringComparison.Ordinal) || !step.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        condition = step[prefix.Length..^1].Trim();
        return condition.Length > 0;
    }

    private bool TryParseRepeatHeader(string step, out int count)
    {
        count = 0;
        if (!step.EndsWith("{", StringComparison.Ordinal) || !step.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var countToken = ResolveVariables(step[7..^1].Trim());
        if (!int.TryParse(countToken, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out count) || count < 0)
        {
            throw new InvalidOperationException("Repeat count must be an integer >= 0.");
        }

        return true;
    }

    private bool TryParseForHeader(string step, out string variableName, out int start, out int end, out int stepValue)
    {
        variableName = string.Empty;
        start = 0;
        end = 0;
        stepValue = 0;
        if (!step.EndsWith("{", StringComparison.Ordinal) || !step.StartsWith("for ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = step[..^1].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 6 && parts.Length != 8)
        {
            throw new InvalidOperationException("Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {");
        }

        variableName = parts[1];
        EnsureValidVariableName(variableName);
        if (!string.Equals(parts[2], "from", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parts[4], "to", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {");
        }

        start = ParseInteger(ResolveVariables(parts[3]), "for start");
        end = ParseInteger(ResolveVariables(parts[5]), "for end");
        if (parts.Length == 8)
        {
            if (!string.Equals(parts[6], "step", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {");
            }

            stepValue = ParseInteger(ResolveVariables(parts[7]), "for step");
        }
        else
        {
            stepValue = start <= end ? 1 : -1;
        }

        return true;
    }

    private static int ParseInteger(string value, string description)
    {
        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"Invalid {description} '{value}'. Expected integer.");
        }

        return parsed;
    }

    private static int FindBlockEnd(IReadOnlyList<string> steps, int start, int end)
    {
        var depth = 0;
        for (var i = start; i < end; i++)
        {
            var step = steps[i];
            if (step.EndsWith("{", StringComparison.Ordinal)
                && (step.StartsWith("if ", StringComparison.OrdinalIgnoreCase)
                    || RunScriptSyntax.IsElseHeader(step)
                    || step.StartsWith("while ", StringComparison.OrdinalIgnoreCase)
                    || step.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase)
                    || step.StartsWith("for ", StringComparison.OrdinalIgnoreCase)))
            {
                depth++;
                continue;
            }

            if (RunScriptSyntax.IsBlockEndToken(step))
            {
                if (depth == 0)
                {
                    return i;
                }

                depth--;
            }
        }

        throw new InvalidOperationException("Missing closing brace '}'.");
    }

    private bool EvaluateCondition(string condition)
    {
        if (!RunScriptConditionParser.TryParse(condition, out var parsedCondition, out var error) || parsedCondition == null)
        {
            throw new InvalidOperationException(error ?? "Invalid condition syntax.");
        }

        var left = ResolveOperand(parsedCondition.LeftToken);
        var right = ResolveOperand(parsedCondition.RightToken);
        if (parsedCondition.OperatorToken is "==" or "!=")
        {
            var equal = ValuesEqual(left, right);
            return parsedCondition.OperatorToken == "==" ? equal : !equal;
        }

        if (!int.TryParse(left, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var leftInt)
            || !int.TryParse(right, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var rightInt))
        {
            throw new InvalidOperationException($"Operator '{parsedCondition.OperatorToken}' requires numeric operands. Got '{left}' and '{right}'.");
        }

        return parsedCondition.OperatorToken switch
        {
            ">" => leftInt > rightInt,
            ">=" => leftInt >= rightInt,
            "<" => leftInt < rightInt,
            "<=" => leftInt <= rightInt,
            _ => throw new InvalidOperationException($"Unsupported condition operator '{parsedCondition.OperatorToken}'.")
        };
    }

    private string ResolveOperand(string token)
    {
        if (token.StartsWith("$$", StringComparison.Ordinal))
        {
            return Unquote(EditorActionScriptTokens.UnescapeLiteralDollar(token));
        }

        if (token.StartsWith('$'))
        {
            var variableName = token[1..];
            EnsureValidVariableName(variableName);
            if (!_runtimeVariables.TryGetValue(variableName, out var value))
            {
                throw new InvalidOperationException($"Unknown variable '${variableName}'.");
            }

            return Unquote(value);
        }

        return EditorActionScriptTokens.UnescapeLiteralDollar(Unquote(token));
    }

    private string ResolveVariables(string input)
    {
        var output = new System.Text.StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] != '$')
            {
                output.Append(input[i]);
                continue;
            }

            if (i + 1 < input.Length && input[i + 1] == '$')
            {
                output.Append('$');
                i++;
                continue;
            }

            var j = i + 1;
            while (j < input.Length && EditorActionScriptTokens.IsVariableNamePart(input[j]))
            {
                j++;
            }

            var variableName = input[(i + 1)..j];
            EnsureValidVariableName(variableName);
            if (!_runtimeVariables.TryGetValue(variableName, out var value))
            {
                throw new InvalidOperationException($"Unknown variable '${variableName}'.");
            }

            output.Append(value);
            i = j - 1;
        }

        return output.ToString();
    }

    private static bool ValuesEqual(string left, string right)
    {
        if (ScreenPixelColor.TryParse(left, out var leftColor)
            && ScreenPixelColor.TryParse(right, out var rightColor))
        {
            return leftColor.Equals(rightColor);
        }

        if (int.TryParse(left, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var leftInt)
            && int.TryParse(right, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var rightInt))
        {
            return leftInt == rightInt;
        }

        if (bool.TryParse(left, out var leftBool) && bool.TryParse(right, out var rightBool))
        {
            return leftBool == rightBool;
        }

        return string.Equals(left, right, StringComparison.Ordinal);
    }

    private static void EnsureValidVariableName(string variableName)
    {
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            throw new InvalidOperationException($"Invalid variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*");
        }
    }

    private static string Unquote(string input)
    {
        if (input.Length >= 2
            && ((input[0] == '"' && input[^1] == '"')
                || (input[0] == '\'' && input[^1] == '\'')))
        {
            return input[1..^1];
        }

        return input;
    }
}
