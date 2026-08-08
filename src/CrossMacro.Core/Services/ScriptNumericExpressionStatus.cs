
namespace CrossMacro.Core.Services;

/// <summary>
/// Outcome of parsing and evaluating a script numeric expression token via
/// <see cref="ScriptNumericExpression.Evaluate(string, IReadOnlyDictionary{string, string}, string?)"/>.
/// </summary>
public enum ScriptNumericExpressionStatus
{
    /// <summary>
    /// The token is not an expression; callers may treat it as plain text (set's raw fallback applies only here).
    /// </summary>
    NotExpression = 0,

    /// <summary>
    /// Committed to expression syntax but structurally invalid (dangling operator, spaced unary minus, chained operators). Never silent.
    /// </summary>
    Malformed = 1,

    /// <summary>The expression parsed and evaluated successfully.</summary>
    Evaluated = 2,

    /// <summary>
    /// Parsed but evaluation failed (unknown variable, invalid operand, div/mod by zero, out of range).
    /// </summary>
    EvaluationError = 3,
}
