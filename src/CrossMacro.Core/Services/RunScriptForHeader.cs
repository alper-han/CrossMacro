namespace CrossMacro.Core.Services;

/// <summary>
/// Parsed <c>for</c> header; segments stay unresolved (interpreters evaluate via <see cref="ScriptNumericExpression"/>).
/// </summary>
public sealed record RunScriptForHeader(
    string VariableName,
    string StartToken,
    string EndToken,
    string? StepToken,
    bool HasExplicitStep);
