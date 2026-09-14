namespace CrossMacro.Core.Services.Scripting;

/// <summary>
/// Parsed run-script condition expression.
/// </summary>
public sealed record RunScriptCondition(string LeftToken, string OperatorToken, string RightToken);
