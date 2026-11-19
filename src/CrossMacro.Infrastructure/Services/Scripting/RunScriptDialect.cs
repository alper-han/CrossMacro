namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// The two surfaces the one script interpreter serves. <see cref="Expansion"/> compiles a script
/// ahead of time and reports every failure with its step and source line; <see cref="Execution"/>
/// runs it against live playback state and throws bare messages. The wording differences between
/// them are part of the script dialect's published behavior and live only here.
/// </summary>
internal abstract class RunScriptDialect
{
    public static RunScriptDialect Expansion { get; } = new ExpansionDialect();

    public static RunScriptDialect Execution { get; } = new ExecutionDialect();

    public abstract int IterationLimit { get; }

    public abstract string NegativeRepeatCount { get; }

    public abstract string ZeroForStep { get; }

    /// <summary>The loop-limit failure text; shared by every loop kind.</summary>
    public abstract string LoopLimitExceeded { get; }

    /// <summary>Wraps a failure message with the location the surface reports it at.</summary>
    public abstract string FormatFailure(RunScriptStep source, string message);

    /// <summary>Describes the malformed <c>$</c> reference starting at <paramref name="dollarIndex"/>.</summary>
    public abstract string InvalidVariableReference(string input, int dollarIndex);

    /// <summary>Describes a failed condition, given the evaluator's own message and any malformed variable it hit.</summary>
    public abstract string ConditionFailure(ScriptConditionEvaluator.Result result);

    /// <summary>
    /// Resolves one integer operand (a literal or variable reference) to its value. The surfaces differ in
    /// whether an operand may embed variable references in a longer token.
    /// </summary>
    public abstract bool TryResolveInteger(
        RunScriptVariables variables,
        string token,
        string description,
        out int value,
        out string error);

    private sealed class ExpansionDialect : RunScriptDialect
    {
        public override int IterationLimit => ScriptExecutionLimits.StaticExpansionIterations;

        public override string NegativeRepeatCount => "repeat count must be >= 0.";

        public override string ZeroForStep => "for step cannot be 0.";

        public override string LoopLimitExceeded =>
            $"loop iteration limit exceeded ({ScriptExecutionLimits.StaticExpansionIterations}). Check loop exit condition.";

        public override string FormatFailure(RunScriptStep source, string message) =>
            $"{RunScriptTreeReader.BuildSourcePrefix(source)}: {message}";

        public override string InvalidVariableReference(string input, int dollarIndex) =>
            dollarIndex + 1 < input.Length
                ? $"Invalid variable reference '${input[dollarIndex + 1]}'."
                : "Invalid variable reference '$'.";

        public override string ConditionFailure(ScriptConditionEvaluator.Result result) => result.ErrorMessage;

        public override bool TryResolveInteger(
            RunScriptVariables variables,
            string token,
            string description,
            out int value,
            out string error)
        {
            var text = token;
            if (token.StartsWith('$'))
            {
                var name = token[1..];
                if (!ScriptIdentifierSyntax.IsValidVariableName(name))
                {
                    value = 0;
                    error = $"Invalid {description} variable reference '{token}'.";
                    return false;
                }

                if (!variables.TryGetValue(name, out text!))
                {
                    value = 0;
                    error = ScriptMessages.UnknownVariable(name);
                    return false;
                }
            }

            return TryParseInteger(text, description, out value, out error);
        }

        private static bool TryParseInteger(string text, string description, out int value, out string error)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = string.Empty;
                return true;
            }

            error = ScriptMessages.InvalidInteger(description, text);
            return false;
        }
    }

    private sealed class ExecutionDialect : RunScriptDialect
    {
        private const string RepeatCountDescription = "repeat count";
        private const string RepeatCountMustBeInteger = "Repeat count must be an integer >= 0.";

        public override int IterationLimit => ScriptExecutionLimits.RuntimeExecutionIterations;

        public override string NegativeRepeatCount => RepeatCountMustBeInteger;

        public override string ZeroForStep => "For step cannot be 0.";

        public override string LoopLimitExceeded =>
            $"Runtime loop iteration limit exceeded ({ScriptExecutionLimits.RuntimeExecutionIterations}). Check loop exit condition.";

        public override string FormatFailure(RunScriptStep source, string message) => message;

        public override string InvalidVariableReference(string input, int dollarIndex)
        {
            var end = dollarIndex + 1;
            while (end < input.Length && ScriptIdentifierSyntax.IsVariableNamePart(input[end]))
            {
                end++;
            }

            return ScriptMessages.InvalidVariableName(input[(dollarIndex + 1)..end]);
        }

        public override string ConditionFailure(ScriptConditionEvaluator.Result result) =>
            result.InvalidVariableName is { } name ? ScriptMessages.InvalidVariableName(name) : result.ErrorMessage;

        public override bool TryResolveInteger(
            RunScriptVariables variables,
            string token,
            string description,
            out int value,
            out string error)
        {
            value = 0;
            if (!variables.TryResolveText(token, this, out var text, out var resolutionError))
            {
                error = resolutionError.Message;
                return false;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                error = string.Empty;
                return true;
            }

            error = string.Equals(description, RepeatCountDescription, StringComparison.Ordinal)
                ? RepeatCountMustBeInteger
                : ScriptMessages.InvalidInteger(description, text);
            return false;
        }
    }
}
