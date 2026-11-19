namespace CrossMacro.UI.ViewModels.Editor;

internal sealed class ScriptArithmeticFieldState
{
    public bool IsAdvanced { get; set; }

    /// <summary>The action instance this state was derived from; null when idle.</summary>
    public EditorAction? Owner { get; set; }

    public string LeftValue { get; set; } = string.Empty;
    public ScriptNumericSourceType LeftType { get; set; } = ScriptNumericSourceType.Number;
    public ScriptArithmeticOperation Operator { get; set; } = ScriptArithmeticOperation.Add;
    public string RightValue { get; set; } = string.Empty;
    public ScriptNumericSourceType RightType { get; set; } = ScriptNumericSourceType.Number;

    public void Reset()
    {
        IsAdvanced = false;
        Owner = null;
        LeftValue = string.Empty;
        LeftType = ScriptNumericSourceType.Number;
        ResetRightOperand();
    }

    public void ResetRightOperand()
    {
        Operator = ScriptArithmeticOperation.Add;
        RightValue = string.Empty;
        RightType = ScriptNumericSourceType.Number;
    }
}
