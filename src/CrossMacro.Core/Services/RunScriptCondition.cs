namespace CrossMacro.Core.Services;

/// <summary>
/// Parsed run-script condition expression.
/// </summary>
public sealed record class RunScriptCondition(string LeftToken, string OperatorToken, string RightToken);
