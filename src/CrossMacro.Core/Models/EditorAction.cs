using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CrossMacro.Core.Models;

/// <summary>
/// Represents a single action in the macro editor.
/// Provides a user-friendly abstraction over MacroEvent for editing.
/// Implements INotifyPropertyChanged for proper UI binding.
/// </summary>
public class EditorAction : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    private EditorActionType _type;
    private int _x;
    private int _y;
    private bool _isAbsolute = true;
    private MouseButton _button = MouseButton.Left;
    private int _keyCode;
    private int _delayMs;
    private bool _useRandomDelay;
    private int _randomDelayMinMs;
    private int _randomDelayMaxMs;
    private bool _useCurrentPosition;
    private int _scrollAmount = 1;
    private string? _keyName;
    private int _index;
    private string _text = string.Empty;
    private string _scriptVariableName = "i";
    private ScriptValueType _scriptValueType = ScriptValueType.Number;
    private string _scriptValue = "0";
    private ScriptNumericSourceType _scriptNumericSourceType = ScriptNumericSourceType.Number;
    private string _scriptNumericValue = "1";
    private ScriptOperandType _scriptLeftOperandType = ScriptOperandType.VariableReference;
    private string _scriptLeftOperand = "i";
    private ScriptConditionOperator _scriptConditionOperator = ScriptConditionOperator.LessThan;
    private ScriptOperandType _scriptRightOperandType = ScriptOperandType.Number;
    private string _scriptRightOperand = "10";
    private string _forVariableName = "i";
    private ScriptNumericSourceType _forStartType = ScriptNumericSourceType.Number;
    private string _forStartValue = "0";
    private ScriptNumericSourceType _forEndType = ScriptNumericSourceType.Number;
    private string _forEndValue = "10";
    private bool _forHasStep;
    private ScriptNumericSourceType _forStepType = ScriptNumericSourceType.Number;
    private string _forStepValue = "1";
    private bool _preferLegacyScriptText;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    /// <summary>
    /// Unique identifier for this action.
    /// </summary>
    public Guid Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }
    
    /// <summary>
    /// Type of action to perform.
    /// </summary>
    public EditorActionType Type
    {
        get => _type;
        set 
        { 
            if (_type != value)
            {
                _type = value; 
                if (!IsScriptPayloadAction(value))
                {
                    _preferLegacyScriptText = false;
                }
                else if (!string.IsNullOrWhiteSpace(_text))
                {
                    _preferLegacyScriptText = true;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// X coordinate (for mouse actions).
    /// For absolute: screen position. For relative: offset.
    /// </summary>
    public int X
    {
        get => _x;
        set 
        { 
            if (_x != value)
            {
                _x = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Y coordinate (for mouse actions).
    /// For absolute: screen position. For relative: offset.
    /// </summary>
    public int Y
    {
        get => _y;
        set 
        { 
            if (_y != value)
            {
                _y = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Whether coordinates are absolute (true) or relative (false).
    /// Used by mouse actions with coordinates (MouseMove/MouseClick/MouseDown/MouseUp).
    /// </summary>
    public bool IsAbsolute
    {
        get => _isAbsolute;
        set 
        { 
            if (_isAbsolute != value)
            {
                _isAbsolute = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Mouse button (for click/down/up actions).
    /// </summary>
    public MouseButton Button
    {
        get => _button;
        set 
        { 
            if (_button != value)
            {
                _button = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Keyboard key code (for key actions).
    /// Uses Linux input key codes.
    /// </summary>
    public int KeyCode
    {
        get => _keyCode;
        set 
        { 
            if (_keyCode != value)
            {
                _keyCode = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Delay in milliseconds (for Delay action or timing between actions).
    /// </summary>
    public int DelayMs
    {
        get => _delayMs;
        set 
        { 
            if (_delayMs != value)
            {
                _delayMs = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Whether delay should be randomized between min/max bounds.
    /// Only applicable for Delay action.
    /// </summary>
    public bool UseRandomDelay
    {
        get => _useRandomDelay;
        set
        {
            if (_useRandomDelay != value)
            {
                _useRandomDelay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Whether a mouse click should use the current cursor position at playback time.
    /// Only applicable for MouseClick actions.
    /// </summary>
    public bool UseCurrentPosition
    {
        get => _useCurrentPosition;
        set
        {
            if (_useCurrentPosition != value)
            {
                _useCurrentPosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Minimum randomized delay in milliseconds.
    /// </summary>
    public int RandomDelayMinMs
    {
        get => _randomDelayMinMs;
        set
        {
            if (_randomDelayMinMs != value)
            {
                _randomDelayMinMs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Maximum randomized delay in milliseconds.
    /// </summary>
    public int RandomDelayMaxMs
    {
        get => _randomDelayMaxMs;
        set
        {
            if (_randomDelayMaxMs != value)
            {
                _randomDelayMaxMs = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Scroll amount (positive = up/right, negative = down/left).
    /// </summary>
    public int ScrollAmount
    {
        get => _scrollAmount;
        set 
        { 
            if (_scrollAmount != value)
            {
                _scrollAmount = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Human-readable key name for display purposes.
    /// </summary>
    public string? KeyName
    {
        get => _keyName;
        set 
        { 
            if (_keyName != value)
            {
                _keyName = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }
    
    /// <summary>
    /// Index of this action in the list (1-based for display).
    /// </summary>
    public int Index
    {
        get => _index;
        set 
        { 
            if (_index != value)
            {
                _index = value; 
                OnPropertyChanged();
            }
        }
    }
    
    /// <summary>
    /// Text content (for TextInput action).
    /// Each character will be converted to a KeyPress event when saving.
    /// </summary>
    public string Text
    {
        get => _text;
        set 
        { 
            if (_text != value)
            {
                _text = value ?? string.Empty; 
                if (IsScriptPayloadAction(Type))
                {
                    _preferLegacyScriptText = !string.IsNullOrWhiteSpace(_text);
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Indicates whether script serialization should prefer legacy raw Text payload over structured fields.
    /// Used for fallback-parsed script actions until structured fields are edited.
    /// </summary>
    public bool PreferLegacyScriptText
    {
        get => _preferLegacyScriptText;
        set => _preferLegacyScriptText = value;
    }

    /// <summary>
    /// Variable name used by Set/Inc/Dec actions.
    /// </summary>
    public string ScriptVariableName
    {
        get => _scriptVariableName;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (_scriptVariableName == normalized)
            {
                return;
            }

            _scriptVariableName = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Value kind for SetVariable action.
    /// </summary>
    public ScriptValueType ScriptValueType
    {
        get => _scriptValueType;
        set
        {
            if (_scriptValueType == value)
            {
                return;
            }

            _scriptValueType = value;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Value payload for SetVariable action.
    /// </summary>
    public string ScriptValue
    {
        get => _scriptValue;
        set
        {
            var normalized = value ?? string.Empty;
            if (_scriptValue == normalized)
            {
                return;
            }

            _scriptValue = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Numeric source type used by Increment/Decrement/Repeat actions.
    /// </summary>
    public ScriptNumericSourceType ScriptNumericSourceType
    {
        get => _scriptNumericSourceType;
        set
        {
            if (_scriptNumericSourceType == value)
            {
                return;
            }

            _scriptNumericSourceType = value;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Numeric token payload used by Increment/Decrement/Repeat actions.
    /// </summary>
    public string ScriptNumericValue
    {
        get => _scriptNumericValue;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (_scriptNumericValue == normalized)
            {
                return;
            }

            _scriptNumericValue = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Left operand source type for If/While conditions.
    /// </summary>
    public ScriptOperandType ScriptLeftOperandType
    {
        get => _scriptLeftOperandType;
        set
        {
            if (_scriptLeftOperandType == value)
            {
                return;
            }

            _scriptLeftOperandType = value;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Left operand payload for If/While conditions.
    /// </summary>
    public string ScriptLeftOperand
    {
        get => _scriptLeftOperand;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (_scriptLeftOperand == normalized)
            {
                return;
            }

            _scriptLeftOperand = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Condition operator for If/While actions.
    /// </summary>
    public ScriptConditionOperator ScriptConditionOperator
    {
        get => _scriptConditionOperator;
        set
        {
            if (_scriptConditionOperator == value)
            {
                return;
            }

            _scriptConditionOperator = value;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Right operand source type for If/While conditions.
    /// </summary>
    public ScriptOperandType ScriptRightOperandType
    {
        get => _scriptRightOperandType;
        set
        {
            if (_scriptRightOperandType == value)
            {
                return;
            }

            _scriptRightOperandType = value;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Right operand payload for If/While conditions.
    /// </summary>
    public string ScriptRightOperand
    {
        get => _scriptRightOperand;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (_scriptRightOperand == normalized)
            {
                return;
            }

            _scriptRightOperand = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Loop variable name for For action.
    /// </summary>
    public string ForVariableName
    {
        get => _forVariableName;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (_forVariableName == normalized)
            {
                return;
            }

            _forVariableName = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Start value source type for For action.
    /// </summary>
    public ScriptNumericSourceType ForStartType
    {
        get => _forStartType;
        set
        {
            if (_forStartType == value)
            {
                return;
            }

            _forStartType = value;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Start value payload for For action.
    /// </summary>
    public string ForStartValue
    {
        get => _forStartValue;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (_forStartValue == normalized)
            {
                return;
            }

            _forStartValue = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// End value source type for For action.
    /// </summary>
    public ScriptNumericSourceType ForEndType
    {
        get => _forEndType;
        set
        {
            if (_forEndType == value)
            {
                return;
            }

            _forEndType = value;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// End value payload for For action.
    /// </summary>
    public string ForEndValue
    {
        get => _forEndValue;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (_forEndValue == normalized)
            {
                return;
            }

            _forEndValue = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Whether For action has explicit step.
    /// </summary>
    public bool ForHasStep
    {
        get => _forHasStep;
        set
        {
            if (_forHasStep == value)
            {
                return;
            }

            _forHasStep = value;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Step source type for For action.
    /// </summary>
    public ScriptNumericSourceType ForStepType
    {
        get => _forStepType;
        set
        {
            if (_forStepType == value)
            {
                return;
            }

            _forStepType = value;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Step payload for For action.
    /// </summary>
    public string ForStepValue
    {
        get => _forStepValue;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (_forStepValue == normalized)
            {
                return;
            }

            _forStepValue = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }
    
    /// <summary>
    /// Gets a human-readable description of this action.
    /// </summary>
    public string DisplayName => GenerateDisplayName();

    private string GenerateDisplayName()
    {
        return Type switch
        {
            EditorActionType.MouseMove when IsAbsolute => $"Move to ({X}, {Y})",
            EditorActionType.MouseMove => $"Move by ({X:+#;-#;0}, {Y:+#;-#;0})",
            EditorActionType.MouseClick when UseCurrentPosition => $"Click {Button} at current position",
            EditorActionType.MouseClick => $"Click {Button}",
            EditorActionType.MouseDown => $"Hold {Button}",
            EditorActionType.MouseUp => $"Release {Button}",
            EditorActionType.KeyPress => $"Press '{KeyName ?? KeyCode.ToString()}'",
            EditorActionType.KeyDown => $"Hold '{KeyName ?? KeyCode.ToString()}'",
            EditorActionType.KeyUp => $"Release '{KeyName ?? KeyCode.ToString()}'",
            EditorActionType.Delay when UseRandomDelay => $"Wait {RandomDelayMinMs}-{RandomDelayMaxMs}ms (random)",
            EditorActionType.Delay => $"Wait {DelayMs}ms",
            EditorActionType.ScrollVertical => ScrollAmount > 0 ? $"Scroll Up {ScrollAmount}" : $"Scroll Down {Math.Abs(ScrollAmount)}",
            EditorActionType.ScrollHorizontal => ScrollAmount > 0 ? $"Scroll Right {ScrollAmount}" : $"Scroll Left {Math.Abs(ScrollAmount)}",
            EditorActionType.TextInput => string.IsNullOrEmpty(Text) 
                ? "Text Input (empty)" 
                : $"Type \"{(Text.Length > 25 ? Text[..25] + "..." : Text)}\"",
            EditorActionType.SetVariable => UseLegacyScriptTextDisplay
                ? $"Set {Text}"
                : IsValidVariableName(ScriptVariableName)
                    ? $"Set {ScriptVariableName} = {BuildSetValueToken()}"
                    : "Set Variable",
            EditorActionType.IncrementVariable => UseLegacyScriptTextDisplay
                ? $"Inc {Text}"
                : IsValidVariableName(ScriptVariableName)
                    ? $"Inc {ScriptVariableName} by {BuildNumericToken(ScriptNumericSourceType, ScriptNumericValue)}"
                    : "Increment Variable",
            EditorActionType.DecrementVariable => UseLegacyScriptTextDisplay
                ? $"Dec {Text}"
                : IsValidVariableName(ScriptVariableName)
                    ? $"Dec {ScriptVariableName} by {BuildNumericToken(ScriptNumericSourceType, ScriptNumericValue)}"
                    : "Decrement Variable",
            EditorActionType.RepeatBlockStart => UseLegacyScriptTextDisplay
                ? $"Repeat ({Text})"
                : $"Repeat ({BuildNumericToken(ScriptNumericSourceType, ScriptNumericValue)})",
            EditorActionType.IfBlockStart => UseLegacyScriptTextDisplay
                ? $"If ({Text})"
                : $"If ({BuildConditionPreview()})",
            EditorActionType.ElseBlockStart => "Else Block",
            EditorActionType.WhileBlockStart => UseLegacyScriptTextDisplay
                ? $"While ({Text})"
                : $"While ({BuildConditionPreview()})",
            EditorActionType.ForBlockStart => UseLegacyScriptTextDisplay
                ? $"For ({Text})"
                : BuildForPreview(),
            EditorActionType.Break => "Break",
            EditorActionType.Continue => "Continue",
            EditorActionType.BlockEnd => "End Block",
            EditorActionType.RawScriptStep => string.IsNullOrWhiteSpace(Text)
                ? "Raw Script Step"
                : $"Raw Script: {(Text.Length > 40 ? Text[..40] + "..." : Text)}",
            _ => "Unknown Action"
        };
    }
    
    /// <summary>
    /// Validates this action.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid()
    {
        return Type switch
        {
            EditorActionType.Delay when UseRandomDelay =>
                RandomDelayMinMs >= 0
                && RandomDelayMaxMs >= RandomDelayMinMs
                && !(RandomDelayMinMs == 0 && RandomDelayMaxMs == 0),
            EditorActionType.Delay => DelayMs >= 0,
            EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp => KeyCode > 0,
            EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal => ScrollAmount != 0,
            EditorActionType.MouseClick when UseCurrentPosition => !IsAbsolute,
            EditorActionType.TextInput => !string.IsNullOrWhiteSpace(Text),
            EditorActionType.SetVariable => UseLegacyScriptTextDisplay || ValidateSetVariableFields(),
            EditorActionType.IncrementVariable or EditorActionType.DecrementVariable => UseLegacyScriptTextDisplay || ValidateIncDecFields(),
            EditorActionType.RepeatBlockStart => UseLegacyScriptTextDisplay || ValidateRepeatFields(),
            EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart => UseLegacyScriptTextDisplay || ValidateConditionFields(),
            EditorActionType.ForBlockStart => UseLegacyScriptTextDisplay || ValidateForFields(),
            EditorActionType.RawScriptStep => !string.IsNullOrWhiteSpace(Text),
            EditorActionType.ElseBlockStart or EditorActionType.BlockEnd or EditorActionType.Break or EditorActionType.Continue => true,
            _ => true
        };
    }
    
    public EditorAction Clone()
    {
        return new EditorAction
        {
            _id = Guid.NewGuid(), // New ID for clone
            _type = Type,
            _x = X,
            _y = Y,
            _isAbsolute = IsAbsolute,
            _button = Button,
            _keyCode = KeyCode,
            _delayMs = DelayMs,
            _useRandomDelay = UseRandomDelay,
            _randomDelayMinMs = RandomDelayMinMs,
            _randomDelayMaxMs = RandomDelayMaxMs,
            _useCurrentPosition = UseCurrentPosition,
            _scrollAmount = ScrollAmount,
            _keyName = KeyName,
            _text = Text,
            _scriptVariableName = ScriptVariableName,
            _scriptValueType = ScriptValueType,
            _scriptValue = ScriptValue,
            _scriptNumericSourceType = ScriptNumericSourceType,
            _scriptNumericValue = ScriptNumericValue,
            _scriptLeftOperandType = ScriptLeftOperandType,
            _scriptLeftOperand = ScriptLeftOperand,
            _scriptConditionOperator = ScriptConditionOperator,
            _scriptRightOperandType = ScriptRightOperandType,
            _scriptRightOperand = ScriptRightOperand,
            _forVariableName = ForVariableName,
            _forStartType = ForStartType,
            _forStartValue = ForStartValue,
            _forEndType = ForEndType,
            _forEndValue = ForEndValue,
            _forHasStep = ForHasStep,
            _forStepType = ForStepType,
            _forStepValue = ForStepValue,
            _preferLegacyScriptText = PreferLegacyScriptText
        };
    }

    private bool UseLegacyScriptTextDisplay => PreferLegacyScriptText && !string.IsNullOrWhiteSpace(Text);

    private void MarkStructuredScriptEdited()
    {
        if (IsScriptPayloadAction(Type))
        {
            _preferLegacyScriptText = false;
        }
    }

    private static bool IsScriptPayloadAction(EditorActionType type)
    {
        return type is
            EditorActionType.SetVariable
            or EditorActionType.IncrementVariable
            or EditorActionType.DecrementVariable
            or EditorActionType.RepeatBlockStart
            or EditorActionType.IfBlockStart
            or EditorActionType.WhileBlockStart
            or EditorActionType.ForBlockStart;
    }

    private string BuildSetValueToken()
    {
        return ScriptValueType switch
        {
            ScriptValueType.VariableReference => $"${NormalizeVariableToken(ScriptValue)}",
            ScriptValueType.Boolean => bool.TryParse(ScriptValue, out var value)
                ? value.ToString().ToLowerInvariant()
                : ScriptValue,
            _ => ScriptValue
        };
    }

    private string BuildConditionPreview()
    {
        var left = BuildOperandToken(ScriptLeftOperandType, ScriptLeftOperand);
        var right = BuildOperandToken(ScriptRightOperandType, ScriptRightOperand);
        return $"{left} {ToOperatorToken(ScriptConditionOperator)} {right}";
    }

    private string BuildForPreview()
    {
        var variableName = string.IsNullOrWhiteSpace(ForVariableName) ? "i" : ForVariableName;
        var start = BuildNumericToken(ForStartType, ForStartValue);
        var end = BuildNumericToken(ForEndType, ForEndValue);
        if (!ForHasStep)
        {
            return $"For ({variableName}: {start} -> {end})";
        }

        var step = BuildNumericToken(ForStepType, ForStepValue);
        return $"For ({variableName}: {start} -> {end}, step {step})";
    }

    private static string BuildNumericToken(ScriptNumericSourceType sourceType, string value)
    {
        var token = string.IsNullOrWhiteSpace(value) ? "0" : value.Trim();
        return sourceType == ScriptNumericSourceType.VariableReference
            ? $"${NormalizeVariableToken(token)}"
            : token;
    }

    private static string BuildOperandToken(ScriptOperandType operandType, string value)
    {
        var token = value.Trim();
        return operandType == ScriptOperandType.VariableReference
            ? $"${NormalizeVariableToken(token)}"
            : token;
    }

    private static string ToOperatorToken(ScriptConditionOperator op)
    {
        return op switch
        {
            ScriptConditionOperator.Equals => "==",
            ScriptConditionOperator.NotEquals => "!=",
            ScriptConditionOperator.GreaterThan => ">",
            ScriptConditionOperator.GreaterThanOrEqual => ">=",
            ScriptConditionOperator.LessThan => "<",
            ScriptConditionOperator.LessThanOrEqual => "<=",
            _ => "=="
        };
    }

    private bool ValidateSetVariableFields()
    {
        if (!IsValidVariableName(ScriptVariableName))
        {
            return false;
        }

        return ScriptValueType switch
        {
            ScriptValueType.Number => int.TryParse(ScriptValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            ScriptValueType.Boolean => bool.TryParse(ScriptValue, out _),
            ScriptValueType.Text => !string.IsNullOrWhiteSpace(ScriptValue),
            ScriptValueType.VariableReference => IsValidVariableName(ScriptValue),
            _ => false
        };
    }

    private bool ValidateIncDecFields()
    {
        return IsValidVariableName(ScriptVariableName)
            && ValidateNumericToken(ScriptNumericSourceType, ScriptNumericValue);
    }

    private bool ValidateRepeatFields()
    {
        return ValidateNumericToken(ScriptNumericSourceType, ScriptNumericValue);
    }

    private bool ValidateConditionFields()
    {
        return ValidateOperandToken(ScriptLeftOperandType, ScriptLeftOperand)
            && ValidateOperandToken(ScriptRightOperandType, ScriptRightOperand);
    }

    private bool ValidateForFields()
    {
        if (!IsValidVariableName(ForVariableName))
        {
            return false;
        }

        if (!ValidateNumericToken(ForStartType, ForStartValue)
            || !ValidateNumericToken(ForEndType, ForEndValue))
        {
            return false;
        }

        return !ForHasStep || ValidateNumericToken(ForStepType, ForStepValue);
    }

    private static bool ValidateNumericToken(ScriptNumericSourceType sourceType, string token)
    {
        if (sourceType == ScriptNumericSourceType.VariableReference)
        {
            return IsValidVariableName(token);
        }

        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool ValidateOperandToken(ScriptOperandType operandType, string token)
    {
        return operandType switch
        {
            ScriptOperandType.VariableReference => IsValidVariableName(token),
            ScriptOperandType.Number => int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            ScriptOperandType.Boolean => bool.TryParse(token, out _),
            ScriptOperandType.Text => !string.IsNullOrWhiteSpace(token),
            _ => false
        };
    }

    private static bool IsValidVariableName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var name = value.Trim();
        if (name.StartsWith("$", StringComparison.Ordinal))
        {
            name = name[1..];
        }

        if (name.Length == 0)
        {
            return false;
        }

        if (!(name[0] == '_' || char.IsLetter(name[0])))
        {
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            if (!(name[i] == '_' || char.IsLetterOrDigit(name[i])))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeVariableToken(string value)
    {
        var token = value.Trim();
        return token.StartsWith("$", StringComparison.Ordinal) ? token[1..] : token;
    }
}
