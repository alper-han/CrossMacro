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

                        if (RunScriptVariableCommands.TryParseAssignment(step, out var assignment, out var setError))
                        {
                            if (setError is not null)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {setError}");
                            }

                            var resolvedValueResult = ResolveVariables(assignment.ValueToken, variables);
                            if (!resolvedValueResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {resolvedValueResult.ErrorMessage}");
                            }

                            if (!RunScriptVariableCommands.TryEvaluateAssignedValue(resolvedValueResult.Value!, variables, out var storedValue, out var expressionError))
                            {
                                return ScriptExpansionResult.Fail($"{source}: {expressionError}");
                            }

                            variables[assignment.Name] = storedValue;
                            break;
                        }

                        if (RunScriptVariableCommands.TryParseUpdate(step, out var update, out var updateError))
                        {
                            if (updateError is not null)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {updateError}");
                            }

                            _ = variables.TryGetValue(update.Name, out var existingValue);
                            if (!RunScriptVariableCommands.TryReadInteger(update, existingValue, out var existingInt, out var readError))
                            {
                                return ScriptExpansionResult.Fail($"{source}: {readError}");
                            }

                            var amountResult = ResolveIntegerToken(update.AmountToken, variables, update.AmountLabel);
                            if (!amountResult.Success)
                            {
                                return ScriptExpansionResult.Fail($"{source}: {amountResult.ErrorMessage}");
                            }

                            if (!RunScriptVariableCommands.TryApply(update, existingInt, amountResult.Value, out var updatedValue, out var applyError))
                            {
                                return ScriptExpansionResult.Fail($"{source}: {applyError}");
                            }

                            variables[update.Name] = updatedValue.ToString(CultureInfo.InvariantCulture);
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

            if (!ScriptIdentifierSyntax.IsVariableNameStart(next))
            {
                return VariableResolutionResult.Fail($"Invalid variable reference '${next}'.");
            }

            var j = i + 1;
            while (j < input.Length && ScriptIdentifierSyntax.IsVariableNamePart(input[j]))
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
            if (!ScriptIdentifierSyntax.IsValidVariableName(variableName))
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
