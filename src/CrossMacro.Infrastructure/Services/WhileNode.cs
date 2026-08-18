namespace CrossMacro.Infrastructure.Services;

internal sealed record WhileNode(
    RunScriptStep Source,
    ConditionExpression Condition,
    IReadOnlyList<RunScriptNode> Body) : RunScriptNode(Source);
