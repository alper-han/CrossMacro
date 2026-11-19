namespace CrossMacro.Infrastructure.Services.Scripting.Nodes;

internal sealed record IfNode(
    RunScriptStep Source,
    ConditionExpression Condition,
    IReadOnlyList<RunScriptNode> TrueBody,
    RunScriptStep? ElseSource,
    IReadOnlyList<RunScriptNode>? FalseBody) : RunScriptNode(Source)
{
    public override IEnumerable<IReadOnlyList<RunScriptNode>> Blocks => FalseBody is null ? [TrueBody] : [TrueBody, FalseBody];
}
