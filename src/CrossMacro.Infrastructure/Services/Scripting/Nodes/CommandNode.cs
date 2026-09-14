namespace CrossMacro.Infrastructure.Services.Scripting.Nodes;

internal sealed record CommandNode(RunScriptStep Source) : RunScriptNode(Source);
