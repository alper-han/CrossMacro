namespace CrossMacro.Infrastructure.Services;

internal sealed record CommandNode(RunScriptStep Source) : RunScriptNode(Source);
