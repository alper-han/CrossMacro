namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>Parses structural script nodes and owns source diagnostics independently of expansion and event emission.</summary>
internal static class RunScriptTreeReader
{
    internal static ScriptNodeParseResult ParseScriptNodes(IReadOnlyList<RunScriptStep> steps)
    {
        var index = 0;
        var result = ParseBlockNodes(steps, ref index, isTopLevel: true);
        if (!result.Success)
        {
            return result;
        }

        return ScriptNodeParseResult.Ok(result.Nodes!);
    }

    internal static ScriptNodeParseResult ParseBlockNodes(IReadOnlyList<RunScriptStep> steps, ref int index, bool isTopLevel)
    {
        var nodes = new List<RunScriptNode>();

        while (index < steps.Count)
        {
            var entry = steps[index];
            var trimmed = entry.Step.Trim();
            var source = BuildSourcePrefix(entry);

            if (RunScriptSyntax.IsBlockEndToken(trimmed))
            {
                if (isTopLevel)
                {
                    return ScriptNodeParseResult.Fail($"{source}: unexpected closing brace '}}'.");
                }

                index++;
                return ScriptNodeParseResult.Ok(nodes);
            }

            if (RunScriptSyntax.IsElseHeader(trimmed))
            {
                return ScriptNodeParseResult.Fail($"{source}: unexpected 'else' block.");
            }

            if (RunScriptHeaderParser.TryParseRepeatCountToken(trimmed, out var repeatCountToken))
            {
                var repeatSource = entry;
                index++;
                var bodyResult = ParseBlockNodes(steps, ref index, isTopLevel: false);
                if (!bodyResult.Success)
                {
                    return bodyResult;
                }

                nodes.Add(new RepeatNode(repeatSource, repeatCountToken, bodyResult.Nodes!));
                continue;
            }

            if (TryParseIfHeader(trimmed, out var ifCondition, out var ifHeaderError))
            {
                if (ifHeaderError is not null)
                {
                    return ScriptNodeParseResult.Fail($"{source}: {ifHeaderError}");
                }

                var ifSource = entry;
                index++;
                var trueBodyResult = ParseBlockNodes(steps, ref index, isTopLevel: false);
                if (!trueBodyResult.Success)
                {
                    return trueBodyResult;
                }

                RunScriptStep? elseSource = null;
                IReadOnlyList<RunScriptNode>? falseBody = null;
                if (index < steps.Count && RunScriptSyntax.IsElseHeader(steps[index].Step.Trim()))
                {
                    elseSource = steps[index];
                    index++;

                    var falseBodyResult = ParseBlockNodes(steps, ref index, isTopLevel: false);
                    if (!falseBodyResult.Success)
                    {
                        return falseBodyResult;
                    }

                    falseBody = falseBodyResult.Nodes!;
                }

                nodes.Add(new IfNode(ifSource, ifCondition!, trueBodyResult.Nodes!, elseSource, falseBody));
                continue;
            }

            if (TryParseWhileHeader(trimmed, out var whileCondition, out var whileHeaderError))
            {
                if (whileHeaderError is not null)
                {
                    return ScriptNodeParseResult.Fail($"{source}: {whileHeaderError}");
                }

                var whileSource = entry;
                index++;
                var bodyResult = ParseBlockNodes(steps, ref index, isTopLevel: false);
                if (!bodyResult.Success)
                {
                    return bodyResult;
                }

                nodes.Add(new WhileNode(whileSource, whileCondition!, bodyResult.Nodes!));
                continue;
            }

            if (RunScriptHeaderParser.TryParseForHeader(trimmed, out var forHeader, out var forHeaderError))
            {
                if (forHeaderError is not null)
                {
                    return ScriptNodeParseResult.Fail($"{source}: {forHeaderError}");
                }

                var forSource = entry;
                index++;
                var bodyResult = ParseBlockNodes(steps, ref index, isTopLevel: false);
                if (!bodyResult.Success)
                {
                    return bodyResult;
                }

                nodes.Add(new ForNode(
                    forSource,
                    forHeader!.VariableName,
                    forHeader.StartToken,
                    forHeader.EndToken,
                    forHeader.StepToken,
                    forHeader.HasExplicitStep,
                    bodyResult.Nodes!));
                continue;
            }

            if (trimmed.EndsWith('{'))
            {
                return ScriptNodeParseResult.Fail(
                    $"{source}: unsupported block syntax. Expected one of: repeat <count> {{, if <left> <op> <right> {{, while <left> <op> <right> {{, for <var> from <start> to <end> [step <n>] {{");
            }

            nodes.Add(new CommandNode(entry));
            index++;
        }

        if (!isTopLevel)
        {
            return ScriptNodeParseResult.Fail("Missing closing brace '}' for block.");
        }

        return ScriptNodeParseResult.Ok(nodes);
    }

    internal static bool TryParseIfHeader(string step, out ConditionExpression? condition, out string? error)
    {
        condition = null;
        error = null;

        if (!RunScriptHeaderParser.TryReadConditionHeader(step, "if", out var payload))
        {
            return false;
        }

        if (!TryParseConditionExpression(payload, out condition, out error))
        {
            return true;
        }

        return true;
    }

    internal static bool TryParseWhileHeader(string step, out ConditionExpression? condition, out string? error)
    {
        condition = null;
        error = null;

        if (!RunScriptHeaderParser.TryReadConditionHeader(step, "while", out var payload))
        {
            return false;
        }

        if (!TryParseConditionExpression(payload, out condition, out error))
        {
            return true;
        }

        return true;
    }

    internal static bool TryParseConditionExpression(string payload, out ConditionExpression? condition, out string? error)
    {
        condition = null;
        if (!RunScriptConditionParser.TryParse(payload, out var parsedCondition, out error) || parsedCondition == null)
        {
            return false;
        }

        condition = new ConditionExpression(
            parsedCondition.LeftToken,
            parsedCondition.OperatorToken,
            parsedCondition.RightToken);
        return true;
    }

    internal static string BuildSourcePrefix(RunScriptStep entry)
    {
        var index = entry.SourceIndex > 0 ? entry.SourceIndex : 1;
        return entry.SourceLineNumber is not null ? $"Step {index.ToString(CultureInfo.InvariantCulture)} (line {entry.SourceLineNumber.Value.ToString(CultureInfo.InvariantCulture)})"
            : $"Step {index.ToString(CultureInfo.InvariantCulture)}";
    }

    internal sealed class ScriptNodeParseResult
    {
        private ScriptNodeParseResult()
        {
        }

        public bool Success { get; private init; }
        public IReadOnlyList<RunScriptNode>? Nodes { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static ScriptNodeParseResult Ok(IReadOnlyList<RunScriptNode> nodes)
        {
            return new ScriptNodeParseResult
            {
                Success = true,
                Nodes = nodes,
            };
        }

        public static ScriptNodeParseResult Fail(string errorMessage)
        {
            return new ScriptNodeParseResult
            {
                Success = false,
                ErrorMessage = errorMessage,
            };
        }
    }

}
