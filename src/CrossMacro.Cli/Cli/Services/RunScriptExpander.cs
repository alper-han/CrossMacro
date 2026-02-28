using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CrossMacro.Cli.Services;

internal static class RunScriptExpander
{
    public static RunScriptExpandResult Expand(IReadOnlyList<RunStepEntry> steps)
    {
        var parseResult = ParseScriptNodes(steps);
        if (!parseResult.Success)
        {
            return RunScriptExpandResult.Fail(parseResult.ErrorMessage);
        }

        var output = new List<RunStepEntry>();
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var expandResult = ExpandScriptNodes(parseResult.Nodes!, variables, output);
        if (!expandResult.Success)
        {
            return RunScriptExpandResult.Fail(expandResult.ErrorMessage);
        }

        return RunScriptExpandResult.Ok(output);
    }

    private static ScriptNodeParseResult ParseScriptNodes(IReadOnlyList<RunStepEntry> steps)
    {
        var index = 0;
        var result = ParseBlockNodes(steps, ref index, isTopLevel: true);
        if (!result.Success)
        {
            return result;
        }

        return ScriptNodeParseResult.Ok(result.Nodes!);
    }

    private static ScriptNodeParseResult ParseBlockNodes(IReadOnlyList<RunStepEntry> steps, ref int index, bool isTopLevel)
    {
        var nodes = new List<RunScriptNode>();

        while (index < steps.Count)
        {
            var entry = steps[index];
            var trimmed = entry.Step.Trim();
            var source = BuildSourcePrefix(entry);

            if (string.Equals(trimmed, "}", StringComparison.Ordinal))
            {
                if (isTopLevel)
                {
                    return ScriptNodeParseResult.Fail($"{source}: unexpected closing brace '}}'.");
                }

                index++;
                return ScriptNodeParseResult.Ok(nodes);
            }

            if (TryParseRepeatHeader(trimmed, out var repeatCountToken))
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

            if (trimmed.EndsWith("{", StringComparison.Ordinal))
            {
                return ScriptNodeParseResult.Fail($"{source}: unsupported block syntax. Expected: repeat <count> {{");
            }

            nodes.Add(new CommandNode(entry));
            index++;
        }

        if (!isTopLevel)
        {
            return ScriptNodeParseResult.Fail("Missing closing brace '}' for repeat block.");
        }

        return ScriptNodeParseResult.Ok(nodes);
    }

    private static ScriptExpansionResult ExpandScriptNodes(
        IReadOnlyList<RunScriptNode> nodes,
        Dictionary<string, string> variables,
        List<RunStepEntry> output)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case CommandNode command:
                {
                    var step = command.Source.Step.Trim();
                    var source = BuildSourcePrefix(command.Source);

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

                        variables[variableName!] = resolvedValueResult.Value!;
                        break;
                    }

                    var resolvedStepResult = ResolveVariables(step, variables);
                    if (!resolvedStepResult.Success)
                    {
                        return ScriptExpansionResult.Fail($"{source}: {resolvedStepResult.ErrorMessage}");
                    }

                    output.Add(command.Source with { Step = resolvedStepResult.Value! });
                    break;
                }
                case RepeatNode repeat:
                {
                    var source = BuildSourcePrefix(repeat.Source);
                    var repeatCountResult = ResolveRepeatCount(repeat.CountToken, variables);
                    if (!repeatCountResult.Success)
                    {
                        return ScriptExpansionResult.Fail($"{source}: {repeatCountResult.ErrorMessage}");
                    }

                    for (var i = 0; i < repeatCountResult.Value; i++)
                    {
                        var nestedResult = ExpandScriptNodes(repeat.Body, variables, output);
                        if (!nestedResult.Success)
                        {
                            return nestedResult;
                        }
                    }

                    break;
                }
                default:
                    return ScriptExpansionResult.Fail("Internal parser error: unsupported script node.");
            }
        }

        return ScriptExpansionResult.Ok(output);
    }

    private static bool TryParseRepeatHeader(string step, out string countToken)
    {
        countToken = string.Empty;
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 3
            && string.Equals(parts[0], "repeat", StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[2], "{", StringComparison.Ordinal))
        {
            countToken = parts[1];
            return true;
        }

        return false;
    }

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
        if (payload.Length == 0)
        {
            error = "Invalid set syntax. Expected: set <name> <value> or set <name>=<value>.";
            return true;
        }

        var equalIndex = payload.IndexOf('=');
        if (equalIndex >= 0)
        {
            variableName = payload[..equalIndex].Trim();
            variableValue = payload[(equalIndex + 1)..].Trim();
        }
        else
        {
            var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
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

        if (!IsValidVariableName(variableName))
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

    private static VariableResolutionResult ResolveVariables(string input, IReadOnlyDictionary<string, string> variables)
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
                output.Append(ch);
                continue;
            }

            if (i + 1 >= input.Length)
            {
                return VariableResolutionResult.Fail("Invalid variable reference '$'.");
            }

            var next = input[i + 1];
            if (!IsVariableNameStart(next))
            {
                return VariableResolutionResult.Fail($"Invalid variable reference '${next}'.");
            }

            var j = i + 1;
            while (j < input.Length && IsVariableNamePart(input[j]))
            {
                j++;
            }

            var variableName = input[(i + 1)..j];
            if (!variables.TryGetValue(variableName, out var value))
            {
                return VariableResolutionResult.Fail($"Unknown variable '${variableName}'.");
            }

            output.Append(value);
            i = j - 1;
        }

        return VariableResolutionResult.Ok(output.ToString());
    }

    private static RepeatCountResolutionResult ResolveRepeatCount(string token, IReadOnlyDictionary<string, string> variables)
    {
        var resolved = token;
        if (token.StartsWith('$'))
        {
            var variableName = token[1..];
            if (!IsValidVariableName(variableName))
            {
                return RepeatCountResolutionResult.Fail($"Invalid repeat count variable reference '{token}'.");
            }

            if (!variables.TryGetValue(variableName, out resolved!))
            {
                return RepeatCountResolutionResult.Fail($"Unknown variable '${variableName}'.");
            }
        }

        if (!int.TryParse(resolved, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count < 0)
        {
            return RepeatCountResolutionResult.Fail($"Invalid repeat count '{resolved}'. Expected integer >= 0.");
        }

        return RepeatCountResolutionResult.Ok(count);
    }

    private static bool IsValidVariableName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (!IsVariableNameStart(name[0]))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!IsVariableNamePart(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsVariableNameStart(char ch)
    {
        return ch == '_' || char.IsLetter(ch);
    }

    private static bool IsVariableNamePart(char ch)
    {
        return ch == '_' || char.IsLetterOrDigit(ch);
    }

    private static string BuildSourcePrefix(RunStepEntry entry)
    {
        return entry.FileLineNumber.HasValue
            ? $"Step {entry.SourceIndex} (line {entry.FileLineNumber.Value})"
            : $"Step {entry.SourceIndex}";
    }

    private sealed class ScriptNodeParseResult
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
                Nodes = nodes
            };
        }

        public static ScriptNodeParseResult Fail(string errorMessage)
        {
            return new ScriptNodeParseResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }

    private sealed class ScriptExpansionResult
    {
        private ScriptExpansionResult()
        {
        }

        public bool Success { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static ScriptExpansionResult Ok(IReadOnlyList<RunStepEntry> _)
        {
            return new ScriptExpansionResult
            {
                Success = true
            };
        }

        public static ScriptExpansionResult Fail(string errorMessage)
        {
            return new ScriptExpansionResult
            {
                Success = false,
                ErrorMessage = errorMessage
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
                Value = value
            };
        }

        public static VariableResolutionResult Fail(string errorMessage)
        {
            return new VariableResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }

    private sealed class RepeatCountResolutionResult
    {
        private RepeatCountResolutionResult()
        {
        }

        public bool Success { get; private init; }
        public int Value { get; private init; }
        public string ErrorMessage { get; private init; } = string.Empty;

        public static RepeatCountResolutionResult Ok(int value)
        {
            return new RepeatCountResolutionResult
            {
                Success = true,
                Value = value
            };
        }

        public static RepeatCountResolutionResult Fail(string errorMessage)
        {
            return new RepeatCountResolutionResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }

    private abstract record RunScriptNode(RunStepEntry Source);
    private sealed record CommandNode(RunStepEntry Source) : RunScriptNode(Source);
    private sealed record RepeatNode(RunStepEntry Source, string CountToken, IReadOnlyList<RunScriptNode> Body) : RunScriptNode(Source);
}

internal sealed class RunScriptExpandResult
{
    private RunScriptExpandResult()
    {
    }

    public bool Success { get; private init; }
    public IReadOnlyList<RunStepEntry> Steps { get; private init; } = [];
    public string ErrorMessage { get; private init; } = string.Empty;

    public static RunScriptExpandResult Ok(IReadOnlyList<RunStepEntry> steps)
    {
        return new RunScriptExpandResult
        {
            Success = true,
            Steps = steps
        };
    }

    public static RunScriptExpandResult Fail(string errorMessage)
    {
        return new RunScriptExpandResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
