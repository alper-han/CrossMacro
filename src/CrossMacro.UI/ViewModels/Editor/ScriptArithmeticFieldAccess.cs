namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Binds an arithmetic field editor to the model properties that hold its value and left operand type.</summary>
internal sealed class ScriptArithmeticFieldAccess(
    Func<EditorActionType, bool> ownsType,
    Func<EditorAction, string> getValue,
    Action<EditorAction, string> setValue,
    Func<EditorAction, ScriptNumericSourceType> getLeftType,
    Action<EditorAction, ScriptNumericSourceType> setLeftType,
    Func<EditorAction, bool>? isAvailable = null)
{
    public bool OwnsType(EditorActionType type) => ownsType(type);
    public string GetValue(EditorAction action) => getValue(action);
    public void SetValue(EditorAction action, string value) => setValue(action, value);
    public ScriptNumericSourceType GetLeftType(EditorAction action) => getLeftType(action);
    public void SetLeftType(EditorAction action, ScriptNumericSourceType type) => setLeftType(action, type);
    public bool IsAvailable(EditorAction action) => isAvailable?.Invoke(action) ?? true;
}
