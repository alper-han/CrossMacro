namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>Owns static variable state, control flow and bounded command expansion.</summary>
internal static class RunScriptStaticExpander
{
    public sealed record Result(bool Success, List<RunScriptStep> Steps, string ErrorMessage)
    {
        public static Result Expanded(List<RunScriptStep> steps) => new(
            Success: true,
            Steps: steps,
            ErrorMessage: string.Empty);

        public static Result Failed(List<RunScriptStep> steps, string error) => new(
            Success: false,
            Steps: steps,
            ErrorMessage: error);
    }

    public static Result Expand(IReadOnlyList<RunScriptNode> nodes)
    {
        var steps = new List<RunScriptStep>();
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = ExpandScriptNodes(nodes, variables, steps, new LoopExecutionState(), loopDepth: 0);
        if (!result.Success)
        {
            return Result.Failed(steps, result.ErrorMessage);
        }
        if (result.LoopControlSignal is not LoopControlSignal.None)
        {
            return Result.Failed(steps, "Internal parser error: unhandled loop-control signal.");
        }
        return Result.Expanded(steps);
    }

    private static ScriptExpansionResult ExpandScriptNodes(
        IReadOnlyList<RunScriptNode> nodes,
        Dictionary<string, string> variables,
        List<RunScriptStep> output,
        LoopExecutionState loopState,
        int loopDepth)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CommandNode command:
                    {
                        var rawStep = command.Source.Step;
                        var step = rawStep.Trim();
                        var source = RunScriptTreeReader.BuildSourcePrefix(command.Source);

                        if (RunScriptSyntax.IsBreakCommand(step))
                        {
                            if (loopDepth is 0)
                            {
                                return ScriptExpansionResult.Fail($"{source}: 'break' can only be used inside repeat/while/for blocks.");
                            }

                            return ScriptExpansionResult.Break();
                        }

                        if (RunScriptSyntax.IsContinueCommand(step))
                        {
                            if (loopDepth is 0)
                            {
                                return ScriptExpansionResult.Fail($"{source}: 'continue' can only be used inside repeat/while/for blocks.");
                            }

                            return ScriptExpansionResult.Continue();
                        }

                        if (TryParseSetCommand(step, out var variableName, out var variableValue, out var setError))
                        {
                            if (!string.IsNullOrEmpty(setError))
                            {
                                return ScriptExpansionResult.Fail($"{source}: {setError}");
                            }

                            var resolvedValueResult = ResolveVariables(variableValue, variables);
                            if (!resolvedValueResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {resolvedValueResult.ErrorMessage}");
                            }

                            // A surviving '$' is a '$$' escape; keep the raw-string fallback.
                            var resolvedValue = resolvedValueResult.Value!;
                            if (!resolvedValue.Contains('$', StringComparison.Ordinal)
                                && ScriptNumericExpression.TryParse(resolvedValue, out var numericExpression)
                                && numericExpression is not null)
                            {
                                if (!ScriptNumericExpression.Evaluate(numericExpression, variables, out var numericValue, out var expressionError))
                                {
                                    return ScriptExpansionResult.Fail($"{source}: {expressionError}");
                                }

                                variables[variableName!] = numericValue.ToString(CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                variables[variableName!] = resolvedValue;
                            }

                            break;
                        }

                        if (TryParseIncDecCommand(step, out var targetVariableName, out var amountToken, out var sign, out var incDecError))
                        {
                            if (incDecError is not null)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {incDecError}");
                            }

                            if (!variables.TryGetValue(targetVariableName!, out var existingValue)
                                || !int.TryParse(existingValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var existingInt))
                            {
                                return ScriptExpansionResult.Fail($"{source}: variable '{targetVariableName}' must exist and be an integer for inc/dec.");
                            }

                            var amountResult = ResolveIntegerToken(amountToken!, variables, "inc/dec amount");
                            if (!amountResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {amountResult.ErrorMessage}");
                            }

                            var updated = existingInt + (sign * amountResult.Value);
                            variables[targetVariableName!] = updated.ToString(CultureInfo.InvariantCulture);
                            break;
                        }

                        if (TryParseMulDivCommand(step, out var mulDivVariableName, out var mulDivAmountToken, out var isDivide, out var mulDivError))
                        {
                            if (mulDivError is not null)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {mulDivError}");
                            }

                            if (!variables.TryGetValue(mulDivVariableName!, out var mulDivExistingValue)
                                || !int.TryParse(mulDivExistingValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mulDivExistingInt))
                            {
                                return ScriptExpansionResult.Fail($"{source}: variable '{mulDivVariableName}' must exist and be an integer for mul/div.");
                            }

                            var mulDivAmountResult = ResolveIntegerToken(mulDivAmountToken!, variables, "mul/div amount");
                            if (!mulDivAmountResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {mulDivAmountResult.ErrorMessage}");
                            }

                            if (isDivide && mulDivAmountResult.Value is 0)
                            {
                                return ScriptExpansionResult.Fail($"{source}: Division by zero is not allowed in mul/div.");
                            }

                            var mulDivResult = isDivide
                                ? (long)mulDivExistingInt / mulDivAmountResult.Value
                                : (long)mulDivExistingInt * mulDivAmountResult.Value;
                            if (mulDivResult is < int.MinValue or > int.MaxValue)
                            {
                                return ScriptExpansionResult.Fail($"{source}: Result is out of range for mul/div.");
                            }

                            variables[mulDivVariableName!] = ((int)mulDivResult).ToString(CultureInfo.InvariantCulture);
                            break;
                        }

                        var resolvedStepResult = ResolveVariables(rawStep, variables);
                        if (!resolvedStepResult.Success)
                        {
                            return ScriptExpansionResult.Fail($"{source}: {resolvedStepResult.ErrorMessage}");
                        }

                        output.Add(command.Source with { Step = resolvedStepResult.Value! });
                        break;
                    }
                case RepeatNode repeat:
                    {
                        var source = RunScriptTreeReader.BuildSourcePrefix(repeat.Source);
                        var repeatCountResult = ResolveBlockArgumentToken(repeat.CountToken, variables, "repeat count");
                        if (!repeatCountResult.Success)
                        {
                            return ScriptExpansionResult.Fail($"{source}: {repeatCountResult.ErrorMessage}");
                        }

                        if (repeatCountResult.Value < 0)
                        {
                            return ScriptExpansionResult.Fail($"{source}: repeat count must be >= 0.");
                        }

                        for (var i = 0; i < repeatCountResult.Value; i++)
                        {
                            if (!TryAdvanceLoopIteration(loopState, source, out var limitError))
                            {
                                return ScriptExpansionResult.Fail(limitError!);
                            }

                            var nestedResult = ExpandScriptNodes(repeat.Body, variables, output, loopState, loopDepth + 1);
                            if (!nestedResult.Success)
                            {
                                return nestedResult;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Break)
                            {
                                break;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Continue)
                            {
                                continue;
                            }
                        }

                        break;
                    }
                case IfNode ifNode:
                    {
                        var source = RunScriptTreeReader.BuildSourcePrefix(ifNode.Source);
                        var conditionResult = EvaluateCondition(ifNode.Condition, variables);
                        if (!conditionResult.Success)
                        {
                            return ScriptExpansionResult.Fail($"{source}: {conditionResult.ErrorMessage}");
                        }

                        var branch = conditionResult.Value ? ifNode.TrueBody : ifNode.FalseBody;
                        if (branch is null || branch.Count is 0)
                        {
                            break;
                        }

                        var nestedResult = ExpandScriptNodes(branch, variables, output, loopState, loopDepth);
                        if (!nestedResult.Success)
                        {
                            return nestedResult;
                        }

                        if (nestedResult.LoopControlSignal is not LoopControlSignal.None)
                        {
                            return nestedResult;
                        }

                        break;
                    }
                case WhileNode whileNode:
                    {
                        var source = RunScriptTreeReader.BuildSourcePrefix(whileNode.Source);
                        while (true)
                        {
                            var conditionResult = EvaluateCondition(whileNode.Condition, variables);
                            if (!conditionResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {conditionResult.ErrorMessage}");
                            }

                            if (!conditionResult.Value)
                            {
                                break;
                            }

                            if (!TryAdvanceLoopIteration(loopState, source, out var limitError))
                            {
                                return ScriptExpansionResult.Fail(limitError!);
                            }

                            var nestedResult = ExpandScriptNodes(whileNode.Body, variables, output, loopState, loopDepth + 1);
                            if (!nestedResult.Success)
                            {
                                return nestedResult;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Break)
                            {
                                break;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Continue)
                            {
                                continue;
                            }
                        }

                        break;
                    }
                case ForNode forNode:
                    {
                        var source = RunScriptTreeReader.BuildSourcePrefix(forNode.Source);
                        var startResult = ResolveBlockArgumentToken(forNode.StartToken, variables, "for start");
                        if (!startResult.Success)
                        {
                            return ScriptExpansionResult.Fail($"{source}: {startResult.ErrorMessage}");
                        }

                        var endResult = ResolveBlockArgumentToken(forNode.EndToken, variables, "for end");
                        if (!endResult.Success)
                        {
                            return ScriptExpansionResult.Fail($"{source}: {endResult.ErrorMessage}");
                        }

                        int stepValue;
                        if (forNode.HasExplicitStep)
                        {
                            var stepResult = ResolveBlockArgumentToken(forNode.StepToken!, variables, "for step");
                            if (!stepResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {stepResult.ErrorMessage}");
                            }

                            stepValue = stepResult.Value;
                        }
                        else
                        {
                            stepValue = startResult.Value <= endResult.Value ? 1 : -1;
                        }

                        if (stepValue is 0)
                        {
                            return ScriptExpansionResult.Fail($"{source}: for step cannot be 0.");
                        }

                        for (var i = startResult.Value;
                             stepValue > 0 ? i <= endResult.Value : i >= endResult.Value;
                             i += stepValue)
                        {
                            if (!TryAdvanceLoopIteration(loopState, source, out var limitError))
                            {
                                return ScriptExpansionResult.Fail(limitError!);
                            }

                            variables[forNode.VariableName] = i.ToString(CultureInfo.InvariantCulture);
                            var nestedResult = ExpandScriptNodes(forNode.Body, variables, output, loopState, loopDepth + 1);
                            if (!nestedResult.Success)
                            {
                                return nestedResult;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Break)
                            {
                                break;
                            }

                            if (nestedResult.LoopControlSignal is LoopControlSignal.Continue)
                            {
                                continue;
                            }
                        }

                        break;
                    }
                default:
                    return ScriptExpansionResult.Fail("Internal parser error: unsupported script node.");
            }
        }

        return ScriptExpansionResult.Ok();
    }

    private static bool TryAdvanceLoopIteration(LoopExecutionState loopState, string source, out string? error)
    {
        loopState.Iterations++;
        if (loopState.Iterations > ScriptExecutionLimits.StaticExpansionIterations)
        {
            error = $"{source}: loop iteration limit exceeded ({ScriptExecutionLimits.StaticExpansionIterations}). Check loop exit condition.";
            return false;
        }

        error = null;
        return true;
    }

    private static ScriptConditionEvaluator.Result EvaluateCondition(ConditionExpression condition, IReadOnlyDictionary<string, string> variables) =>
        ScriptConditionEvaluator.Evaluate(condition.LeftToken, condition.OperatorToken, condition.RightToken, variables);

    private static bool TryParseSetCommand(string step, out string? variableName, out string variableValue, out string? error)
    {
        variableName = null;
        variableValue = string.Empty;
        error = null;

        if (!step.StartsWith("set ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var payload = step[4..].Trim();
        if (payload.Length is 0)
        {
            error = "Invalid set syntax. Expected: set <name> <value> or set <name>=<value>.";
            return true;
        }

        var equalIndex = payload.IndexOf('=', StringComparison.Ordinal);
        if (equalIndex >= 0)
        {
            variableName = payload[..equalIndex].Trim();
            variableValue = payload[(equalIndex + 1)..].Trim();
        }
        else
        {
            var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is not 2)
            {
                error = "Invalid set syntax. Expected: set <name> <value> or set <name>=<value>.";
                return true;
            }

            variableName = parts[0];
            variableValue = parts[1];
        }

        if (string.IsNullOrWhiteSpace(variableName))
        {
            error = "Variable name cannot be empty.";
            return true;
        }

        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            error = $"Invalid variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return true;
        }

        if (string.IsNullOrWhiteSpace(variableValue))
        {
            error = $"Variable '{variableName}' value cannot be empty.";
            return true;
        }

        return true;
    }

    private static bool TryParseIncDecCommand(
        string step,
        out string? variableName,
        out string? amountToken,
        out int sign,
        out string? error)
    {
        variableName = null;
        amountToken = null;
        sign = 0;
        error = null;

        if (!step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase)
            && !step.StartsWith("dec ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        sign = step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        var command = sign > 0 ? "inc" : "dec";
        var payload = step[4..].Trim();
        var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length is < 1 or > 2)
        {
            error = $"Invalid {command} syntax. Expected: {command} <name> [amount].";
            return true;
        }

        variableName = parts[0];
        amountToken = parts.Length is 2 ? parts[1] : "1";
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            error = $"Invalid variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return true;
        }

        return true;
    }

    private static bool TryParseMulDivCommand(
        string step,
        out string? variableName,
        out string? amountToken,
        out bool isDivide,
        out string? error)
    {
        variableName = null;
        amountToken = null;
        isDivide = false;
        error = null;

        if (!step.StartsWith("mul ", StringComparison.OrdinalIgnoreCase)
            && !step.StartsWith("div ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        isDivide = step.StartsWith("div ", StringComparison.OrdinalIgnoreCase);
        var command = isDivide ? "div" : "mul";
        var payload = step[4..].Trim();
        var parts = payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length is < 1 or > 2)
        {
            error = $"Invalid {command} syntax. Expected: {command} <name> [amount].";
            return true;
        }

        variableName = parts[0];
        amountToken = parts.Length is 2 ? parts[1] : "1";
        if (!EditorActionScriptTokens.IsValidVariableName(variableName))
        {
            error = $"Invalid variable name '{variableName}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            return true;
        }

        return true;
    }

    private static VariableResolutionResult ResolveVariables(string input, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(input))
        {
            return VariableResolutionResult.Ok(input);
        }

        var output = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch != '$')
            {
                _ = output.Append(ch);
                continue;
            }

            if (i + 1 >= input.Length)
            {
                return VariableResolutionResult.Fail("Invalid variable reference '$'.");
            }

            var next = input[i + 1];
            if (next == '$')
            {
                _ = output.Append('$');
                i++;
                continue;
            }

            if (!EditorActionScriptTokens.IsVariableNameStart(next))
            {
                return VariableResolutionResult.Fail($"Invalid variable reference '${next}'.");
            }

            var j = i + 1;
            while (j < input.Length && EditorActionScriptTokens.IsVariableNamePart(input[j]))
            {
                j++;
            }

            var variableName = input[(i + 1)..j];
            if (!variables.TryGetValue(variableName, out var value))
            {
                return VariableResolutionResult.Fail($"Unknown variable '${variableName}'.");
            }

            _ = output.Append(value);
            i = j - 1;
        }

        return VariableResolutionResult.Ok(output.ToString());
    }

    private static IntegerResolutionResult ResolveBlockArgumentToken(
        string token,
        Dictionary<string, string> variables,
        string description)
    {
        // Block arguments accept one binary expression via the Core authority; committed expressions fail loudly, plain tokens keep the legacy path.
        var evaluation = ScriptNumericExpression.Evaluate(token, variables, description);
        return evaluation.Status switch
        {
            ScriptNumericExpressionStatus.Evaluated => IntegerResolutionResult.Ok(evaluation.Value),
            ScriptNumericExpressionStatus.NotExpression => ResolveIntegerToken(token, variables, description),
            ScriptNumericExpressionStatus.Malformed or ScriptNumericExpressionStatus.EvaluationError => IntegerResolutionResult.Fail(evaluation.Error!),
            _ => IntegerResolutionResult.Fail(evaluation.Error!),
        };
    }

    private static IntegerResolutionResult ResolveIntegerToken(
        string token,
        Dictionary<string, string> variables,
        string description)
    {
        var resolved = token;
        if (token.StartsWith('$'))
        {
            var variableName = token[1..];
            if (!EditorActionScriptTokens.IsValidVariableName(variableName))
            {
                return IntegerResolutionResult.Fail($"Invalid {description} variable reference '{token}'.");
            }

            if (!variables.TryGetValue(variableName, out resolved!))
            {
                return IntegerResolutionResult.Fail($"Unknown variable '${variableName}'.");
            }
        }

        if (!int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return IntegerResolutionResult.Fail($"Invalid {description} '{resolved}'. Expected integer.");
        }

        return IntegerResolutionResult.Ok(parsed);
    }

    private sealed class LoopExecutionState
    {
        public int Iterations { get; set; }
    }

    private enum LoopControlSignal
    {
        None = 0,
        Break = 1,
        Continue = 2,
    }

    private sealed class ScriptExpansionResult
    {
        private ScriptExpansionResult()
        {
        }

        public bool Success { get; private init; }
        public LoopControlSignal LoopControlSignal { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static ScriptExpansionResult Ok()
        {
            return new ScriptExpansionResult
            {
                Success = true,
                LoopControlSignal = LoopControlSignal.None,
            };
        }

        public static ScriptExpansionResult Fail(string errorMessage)
        {
            return new ScriptExpansionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }

        public static ScriptExpansionResult Break()
        {
            return new ScriptExpansionResult
            {
                Success = true,
                LoopControlSignal = LoopControlSignal.Break,
            };
        }

        public static ScriptExpansionResult Continue()
        {
            return new ScriptExpansionResult
            {
                Success = true,
                LoopControlSignal = LoopControlSignal.Continue,
            };
        }
    }

    private sealed class VariableResolutionResult
    {
        private VariableResolutionResult()
        {
        }

        public bool Success { get; private init; }
        public string? Value { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static VariableResolutionResult Ok(string value)
        {
            return new VariableResolutionResult
            {
                Success = true,
                Value = value,
            };
        }

        public static VariableResolutionResult Fail(string errorMessage)
        {
            return new VariableResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }
    }

    private sealed class IntegerResolutionResult
    {
        private IntegerResolutionResult()
        {
        }

        public bool Success { get; private init; }
        public int Value { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static IntegerResolutionResult Ok(int value)
        {
            return new IntegerResolutionResult
            {
                Success = true,
                Value = value,
            };
        }

        public static IntegerResolutionResult Fail(string errorMessage)
        {
            return new IntegerResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }
    }

}
