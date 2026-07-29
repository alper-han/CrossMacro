namespace CrossMacro.Infrastructure.Services;

internal sealed record ForNode(
    RunScriptStep Source,
    string VariableName,
    string StartToken,
    string EndToken,
    string? StepToken,
    bool HasExplicitStep,
    IReadOnlyList<RunScriptNode> Body) : RunScriptNode(Source);
