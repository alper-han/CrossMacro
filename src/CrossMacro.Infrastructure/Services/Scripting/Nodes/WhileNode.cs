namespace CrossMacro.Infrastructure.Services.Scripting.Nodes;

internal sealed record WhileNode(
    RunScriptStep Source,
    ConditionExpression Condition,
    IReadOnlyList<RunScriptNode> Body) : RunScriptNode(Source);
