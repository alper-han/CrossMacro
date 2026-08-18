
namespace CrossMacro.Core.Services;

/// <summary>
/// Structured evaluation outcome: <see cref="Status"/>, <see cref="Value"/> (0 unless Evaluated),
/// canonical <see cref="Error"/> text (null unless Malformed or EvaluationError).
/// </summary>
public sealed record ScriptNumericEvaluationResult(
    ScriptNumericExpressionStatus Status,
    int Value,
    string? Error);
