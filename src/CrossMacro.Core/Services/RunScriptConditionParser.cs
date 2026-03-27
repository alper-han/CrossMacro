using System;

namespace CrossMacro.Core.Services;

/// <summary>
/// Parsed run-script condition expression.
/// </summary>
public sealed record RunScriptCondition(string LeftToken, string OperatorToken, string RightToken);

/// <summary>
/// Shared parser for run-script condition expressions.
/// </summary>
public static class RunScriptConditionParser
{
    private static readonly string[] Operators = [">=", "<=", "==", "!=", ">", "<"];

    public static bool TryParse(string payload, out RunScriptCondition? condition, out string? error)
    {
        condition = null;
        error = null;

        if (string.IsNullOrWhiteSpace(payload))
        {
            error = "Condition cannot be empty.";
            return false;
        }

        var bestOperator = string.Empty;
        var bestOperatorIndex = -1;
        var bestBoundaryScore = -1;
        var sawInvalidBoundaryCandidate = false;

        foreach (var op in Operators)
        {
            var searchIndex = 0;
            while (searchIndex < payload.Length)
            {
                var opIndex = payload.IndexOf(op, searchIndex, StringComparison.Ordinal);
                if (opIndex < 0)
                {
                    break;
                }

                if (opIndex == 0)
                {
                    sawInvalidBoundaryCandidate = true;
                    searchIndex = opIndex + op.Length;
                    continue;
                }

                var leftToken = payload[..opIndex].Trim();
                var rightToken = payload[(opIndex + op.Length)..].Trim();
                if (leftToken.Length == 0 || rightToken.Length == 0)
                {
                    sawInvalidBoundaryCandidate = true;
                    searchIndex = opIndex + op.Length;
                    continue;
                }

                var boundaryScore = 0;
                if (opIndex > 0 && char.IsWhiteSpace(payload[opIndex - 1]))
                {
                    boundaryScore++;
                }

                var rightBoundaryIndex = opIndex + op.Length;
                if (rightBoundaryIndex < payload.Length && char.IsWhiteSpace(payload[rightBoundaryIndex]))
                {
                    boundaryScore++;
                }

                if (boundaryScore > bestBoundaryScore
                    || (boundaryScore == bestBoundaryScore && (bestOperatorIndex < 0 || opIndex < bestOperatorIndex)))
                {
                    bestBoundaryScore = boundaryScore;
                    bestOperatorIndex = opIndex;
                    bestOperator = op;
                }

                searchIndex = opIndex + op.Length;
            }
        }

        if (bestOperatorIndex > 0)
        {
            var leftToken = payload[..bestOperatorIndex].Trim();
            var rightToken = payload[(bestOperatorIndex + bestOperator.Length)..].Trim();
            condition = new RunScriptCondition(leftToken, bestOperator, rightToken);
            return true;
        }

        if (sawInvalidBoundaryCandidate)
        {
            error = "Condition must be in the form: <left> <op> <right>.";
            return false;
        }

        error = "Unsupported condition operator. Allowed: ==, !=, >, >=, <, <=.";
        return false;
    }
}
