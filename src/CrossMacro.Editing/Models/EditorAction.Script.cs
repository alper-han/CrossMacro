namespace CrossMacro.Editing.Models;

public partial class EditorAction
{
    /// <summary>
    /// Indicates whether script serialization should prefer legacy raw Text payload over structured fields.
    /// Used for fallback-parsed script actions until structured fields are edited.
    /// </summary>
    public bool PreferLegacyScriptText { get => _script.PreferLegacyScriptText; set => _script.PreferLegacyScriptText = value; }

    /// <summary>
    /// Variable name used by Set/Inc/Dec actions.
    /// </summary>
    public string ScriptVariableName
    {
        get => _script.ScriptVariableName;
        set => SetScriptField(ref _script.ScriptVariableName, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Shortcut used by <see cref="EditorActionType.CopySelectionToVariable"/>.
    /// </summary>
    public ClipboardCopyShortcut ClipboardCopyShortcut
    {
        get => _script.ClipboardCopyShortcut;
        set => SetScriptField(ref _script.ClipboardCopyShortcut, value);
    }

    /// <summary>
    /// Variable name that receives the current logical cursor X coordinate.
    /// </summary>
    public string MousePositionXVariableName
    {
        get => _script.MousePositionXVariableName;
        set => SetScriptField(ref _script.MousePositionXVariableName, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Variable name that receives the current logical cursor Y coordinate.
    /// </summary>
    public string MousePositionYVariableName
    {
        get => _script.MousePositionYVariableName;
        set => SetScriptField(ref _script.MousePositionYVariableName, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Value kind for SetVariable action.
    /// </summary>
    public ScriptValueType ScriptValueType
    {
        get => _script.ScriptValueType;
        set => SetScriptField(ref _script.ScriptValueType, value);
    }

    /// <summary>
    /// Value payload for SetVariable action.
    /// </summary>
    public string ScriptValue
    {
        get => _script.ScriptValue;
        set => SetScriptField(ref _script.ScriptValue, value ?? string.Empty);
    }

    /// <summary>
    /// Numeric source type used by Increment/Decrement/Repeat actions.
    /// </summary>
    public ScriptNumericSourceType ScriptNumericSourceType
    {
        get => _script.ScriptNumericSourceType;
        set => SetScriptField(ref _script.ScriptNumericSourceType, value);
    }

    /// <summary>
    /// Numeric token payload used by Increment/Decrement/Repeat actions.
    /// </summary>
    public string ScriptNumericValue
    {
        get => _script.ScriptNumericValue;
        set => SetScriptField(ref _script.ScriptNumericValue, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Left operand source type for If/While conditions.
    /// </summary>
    public ScriptOperandType ScriptLeftOperandType
    {
        get => _script.ScriptLeftOperandType;
        set => SetScriptField(ref _script.ScriptLeftOperandType, value);
    }

    /// <summary>
    /// Left operand payload for If/While conditions.
    /// </summary>
    public string ScriptLeftOperand
    {
        get => _script.ScriptLeftOperand;
        set => SetScriptField(ref _script.ScriptLeftOperand, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Condition operator for If/While actions.
    /// </summary>
    public ScriptConditionOperator ScriptConditionOperator
    {
        get => _script.ScriptConditionOperator;
        set => SetScriptField(ref _script.ScriptConditionOperator, value);
    }

    /// <summary>
    /// Right operand source type for If/While conditions.
    /// </summary>
    public ScriptOperandType ScriptRightOperandType
    {
        get => _script.ScriptRightOperandType;
        set => SetScriptField(ref _script.ScriptRightOperandType, value);
    }

    /// <summary>
    /// Right operand payload for If/While conditions.
    /// </summary>
    public string ScriptRightOperand
    {
        get => _script.ScriptRightOperand;
        set => SetScriptField(ref _script.ScriptRightOperand, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Loop variable name for For action.
    /// </summary>
    public string ForVariableName
    {
        get => _script.ForVariableName;
        set => SetScriptField(ref _script.ForVariableName, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Start value source type for For action.
    /// </summary>
    public ScriptNumericSourceType ForStartType
    {
        get => _script.ForStartType;
        set => SetScriptField(ref _script.ForStartType, value);
    }

    /// <summary>
    /// Start value payload for For action.
    /// </summary>
    public string ForStartValue
    {
        get => _script.ForStartValue;
        set => SetScriptField(ref _script.ForStartValue, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// End value source type for For action.
    /// </summary>
    public ScriptNumericSourceType ForEndType
    {
        get => _script.ForEndType;
        set => SetScriptField(ref _script.ForEndType, value);
    }

    /// <summary>
    /// End value payload for For action.
    /// </summary>
    public string ForEndValue
    {
        get => _script.ForEndValue;
        set => SetScriptField(ref _script.ForEndValue, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Whether For action has explicit step.
    /// </summary>
    public bool ForHasStep
    {
        get => _script.ForHasStep;
        set => SetScriptField(ref _script.ForHasStep, value);
    }

    /// <summary>
    /// Step source type for For action.
    /// </summary>
    public ScriptNumericSourceType ForStepType
    {
        get => _script.ForStepType;
        set => SetScriptField(ref _script.ForStepType, value);
    }

    /// <summary>
    /// Step payload for For action.
    /// </summary>
    public string ForStepValue
    {
        get => _script.ForStepValue;
        set => SetScriptField(ref _script.ForStepValue, value?.Trim() ?? string.Empty);
    }
}
