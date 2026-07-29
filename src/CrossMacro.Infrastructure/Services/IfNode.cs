namespace CrossMacro.Infrastructure.Services;

internal sealed record IfNode(
    RunScriptStep Source,
    ConditionExpression Condition,
    IReadOnlyList<RunScriptNode> TrueBody,
    RunScriptStep? ElseSource,
    IReadOnlyList<RunScriptNode>? FalseBody) : RunScriptNode(Source);
