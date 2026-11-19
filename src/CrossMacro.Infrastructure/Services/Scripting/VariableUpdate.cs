namespace CrossMacro.Infrastructure.Services.Scripting;

internal readonly record struct VariableUpdate(VariableUpdateKind Kind, string Name, string AmountToken)
{
    public bool IsAdditive => Kind is VariableUpdateKind.Increment or VariableUpdateKind.Decrement;

    /// <summary>Command family used in diagnostics: <c>inc/dec</c> or <c>mul/div</c>.</summary>
    public string Family => IsAdditive ? "inc/dec" : "mul/div";

    /// <summary>Description of the amount operand used in diagnostics.</summary>
    public string AmountLabel => IsAdditive ? "inc/dec amount" : "mul/div amount";
}
