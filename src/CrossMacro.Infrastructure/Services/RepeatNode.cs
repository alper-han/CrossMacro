namespace CrossMacro.Infrastructure.Services;

internal sealed record RepeatNode(RunScriptStep Source, string CountToken, IReadOnlyList<RunScriptNode> Body) : RunScriptNode(Source);
