namespace CrossMacro.Infrastructure.Services.Scripting.Nodes;

internal sealed record RepeatNode(RunScriptStep Source, string CountToken, IReadOnlyList<RunScriptNode> Body) : RunScriptNode(Source);
