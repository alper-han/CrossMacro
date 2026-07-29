
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
    private MacroMouseButton _button = MacroMouseButton.Left;
    private int _keyCode;
    private int _delayMs;
    private bool _useRandomDelay;
    private int _randomDelayMinMs;
    private int _randomDelayMaxMs;
    private bool _useCurrentPosition;
    private int _scrollAmount = 1;
    private string? _keyName;
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
    private int _screenX;
    private int _screenY;
    private int _screenLeft;
    private int _screenTop;
    private int _screenWidth = 1920;
    private int _screenHeight = 1080;
    private string _screenColorHex = "FFFFFF";
    private EditorActionScreenTargetColorSource _screenTargetColorSource = EditorActionScreenTargetColorSource.ManualHex;
    private string _screenTargetColorVariableName = EditorActionScreenReadingPayload.DefaultTargetColorVariableName;
    private string _screenColorVariableName = "color";
    private int _screenTimeoutMs = 5000;
    private int _screenTolerance;
    private string _screenFoundVariableName = "found";
    private string _screenFoundXVariableName = "found_x";
    private string _screenFoundYVariableName = "found_y";
    private string _imageAssetName = string.Empty;
    private double _imageSearchSimilarity = 1.0;
    private int _imageSearchDownsample = 1;
    private ShellCommandMode _shellCommandMode = ShellCommandMode.Shell;
    private string _shellCommand = string.Empty;
    private string _shellStandardInput = string.Empty;
    private string _shellExitCodeVariableName = "exit_code";
    private string _shellStandardOutputVariableName = "stdout";
    private string _shellStandardErrorVariableName = "stderr";
    private int _shellRetries;
    private int _shellBackoffMs;
    private int _shellTimeoutMs;
    private string _screenshotOutputPath = string.Empty;
    private bool _screenshotCopyToClipboard;
    private bool _screenshotUseRegion;
    private string _screenshotRegionX = "0";
    private string _screenshotRegionY = "0";
    private string _screenshotRegionWidth = "100";
    private string _screenshotRegionHeight = "100";
    private WindowCommandMode _windowCommandMode = WindowCommandMode.Active;
    private string _windowSelectorKind = "title";
    private string _windowSelectorValue = string.Empty;
    private string _windowActiveField = "title";
    private string _windowOutputVariable = "windowResult";
    private int _windowTimeoutMs = 5000;
    private int _windowX;
    private int _windowY;
    private int _windowWidth = 1280;
    private int _windowHeight = 720;
    private string _windowWorkspace = string.Empty;
    private List<MacroEvent>? _preservedTextInputEvents;
    private string? _preservedTextInputText;

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
                if (value is not EditorActionType.TextInput)
                {
                    ClearPreservedTextInputEvents();
                }

                if (!IsScriptPayloadAction(value))
                {
                    PreferLegacyScriptText = false;
                }
                else if (!string.IsNullOrWhiteSpace(_text))
                {
                    PreferLegacyScriptText = true;
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
    public MacroMouseButton Button
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
            if (!string.Equals(_keyName, value, StringComparison.Ordinal))
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
    public int Index { get; set; }

    /// <summary>
    /// Text content (for TextInput action).
    /// Each character will be converted to a KeyPress event when saving.
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            if (!string.Equals(_text, value, StringComparison.Ordinal))
            {
                var normalized = value ?? string.Empty;
                _text = normalized;
                if (Type is EditorActionType.TextInput && !string.Equals(_preservedTextInputText, normalized, StringComparison.Ordinal))
                {
                    ClearPreservedTextInputEvents();
                }

                if (IsScriptPayloadAction(Type))
                {
                    PreferLegacyScriptText = !string.IsNullOrWhiteSpace(_text);
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
    public bool PreferLegacyScriptText { get; set; }

    public IReadOnlyList<MacroEvent>? GetPreservedTextInputEvents()
    {
        return Type is EditorActionType.TextInput
&& _preservedTextInputEvents is { Count: > 0 }
&& string.Equals(_preservedTextInputText, Text
, StringComparison.Ordinal) ? _preservedTextInputEvents
            : null;
    }

    public void PreserveTextInputEvents(IEnumerable<MacroEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _preservedTextInputEvents = events.ToList();
        _preservedTextInputText = Text;
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
            if (string.Equals(_scriptVariableName, normalized, StringComparison.Ordinal))
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
            if (string.Equals(_scriptValue, normalized, StringComparison.Ordinal))
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
            if (string.Equals(_scriptNumericValue, normalized, StringComparison.Ordinal))
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
            if (string.Equals(_scriptLeftOperand, normalized, StringComparison.Ordinal))
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
            if (string.Equals(_scriptRightOperand, normalized, StringComparison.Ordinal))
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
            if (string.Equals(_forVariableName, normalized, StringComparison.Ordinal))
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
            if (string.Equals(_forStartValue, normalized, StringComparison.Ordinal))
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
            if (string.Equals(_forEndValue, normalized, StringComparison.Ordinal))
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
            if (string.Equals(_forStepValue, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _forStepValue = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public int ScreenX
    {
        get => _screenX;
        set => SetScreenField(ref _screenX, value);
    }

    public int ScreenY
    {
        get => _screenY;
        set => SetScreenField(ref _screenY, value);
    }

    public int ScreenLeft
    {
        get => _screenLeft;
        set => SetScreenField(ref _screenLeft, value);
    }

    public int ScreenTop
    {
        get => _screenTop;
        set => SetScreenField(ref _screenTop, value);
    }

    public int ScreenWidth
    {
        get => _screenWidth;
        set => SetScreenField(ref _screenWidth, value);
    }

    public int ScreenHeight
    {
        get => _screenHeight;
        set => SetScreenField(ref _screenHeight, value);
    }

    public string ScreenColorHex
    {
        get => _screenColorHex;
        set => SetScreenField(ref _screenColorHex, NormalizeColorHex(value));
    }

    public EditorActionScreenTargetColorSource ScreenTargetColorSource
    {
        get => _screenTargetColorSource;
        set => SetScreenField(ref _screenTargetColorSource, value);
    }

    public string ScreenTargetColorVariableName
    {
        get => _screenTargetColorVariableName;
        set => SetScreenField(ref _screenTargetColorVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenColorVariableName
    {
        get => _screenColorVariableName;
        set => SetScreenField(ref _screenColorVariableName, value?.Trim() ?? string.Empty);
    }

    public int ScreenTimeoutMs
    {
        get => _screenTimeoutMs;
        set => SetScreenField(ref _screenTimeoutMs, value);
    }

    public int ScreenTolerance
    {
        get => _screenTolerance;
        set => SetScreenField(ref _screenTolerance, value);
    }

    public string ScreenFoundVariableName
    {
        get => _screenFoundVariableName;
        set => SetScreenField(ref _screenFoundVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenFoundXVariableName
    {
        get => _screenFoundXVariableName;
        set => SetScreenField(ref _screenFoundXVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenFoundYVariableName
    {
        get => _screenFoundYVariableName;
        set => SetScreenField(ref _screenFoundYVariableName, value?.Trim() ?? string.Empty);
    }

    public string ImageAssetName
    {
        get => _imageAssetName;
        set => SetScreenField(ref _imageAssetName, value?.Trim() ?? string.Empty);
    }

    public double ImageSearchSimilarity
    {
        get => _imageSearchSimilarity;
        set => SetScreenField(ref _imageSearchSimilarity, value);
    }

    public int ImageSearchDownsample
    {
        get => _imageSearchDownsample;
        set => SetScreenField(ref _imageSearchDownsample, value);
    }

    public bool ImageSearchScaleAware { get; set; }

    public EditorImageMatchMode ImageSearchMatchMode { get; set; }

    public bool ImageSearchMatchModeWasExplicit { get; set; }

    public void SetImageSearchScaleAware(bool value)
    {
        ImageSearchScaleAware = value;
        MarkStructuredScriptEdited();
    }

    public void SetImageSearchMatchMode(EditorImageMatchMode value, bool wasExplicit = true)
    {
        ImageSearchMatchMode = value;
        ImageSearchMatchModeWasExplicit = wasExplicit;
        MarkStructuredScriptEdited();
    }

    public ShellCommandMode ShellCommandMode
    {
        get => _shellCommandMode;
        set => SetScriptField(ref _shellCommandMode, value);
    }

    public string ShellCommand
    {
        get => _shellCommand;
        set => SetScriptField(ref _shellCommand, value ?? string.Empty);
    }

    public string ShellStandardInput
    {
        get => _shellStandardInput;
        set => SetScriptField(ref _shellStandardInput, value ?? string.Empty);
    }

    public string ShellExitCodeVariableName
    {
        get => _shellExitCodeVariableName;
        set => SetScriptField(ref _shellExitCodeVariableName, value?.Trim() ?? string.Empty);
    }

    public string ShellStandardOutputVariableName
    {
        get => _shellStandardOutputVariableName;
        set => SetScriptField(ref _shellStandardOutputVariableName, value?.Trim() ?? string.Empty);
    }

    public string ShellStandardErrorVariableName
    {
        get => _shellStandardErrorVariableName;
        set => SetScriptField(ref _shellStandardErrorVariableName, value?.Trim() ?? string.Empty);
    }

    public int ShellRetries
    {
        get => _shellRetries;
        set => SetScriptField(ref _shellRetries, value);
    }

    public int ShellBackoffMs
    {
        get => _shellBackoffMs;
        set => SetScriptField(ref _shellBackoffMs, value);
    }

    public int ShellTimeoutMs
    {
        get => _shellTimeoutMs;
        set => SetScriptField(ref _shellTimeoutMs, value);
    }

    public string ScreenshotOutputPath
    {
        get => _screenshotOutputPath;
        set => SetScriptField(ref _screenshotOutputPath, value ?? string.Empty);
    }

    public bool ScreenshotCopyToClipboard
    {
        get => _screenshotCopyToClipboard;
        set => SetScriptField(ref _screenshotCopyToClipboard, value);
    }

    public bool ScreenshotUseRegion
    {
        get => _screenshotUseRegion;
        set => SetScriptField(ref _screenshotUseRegion, value);
    }

    public string ScreenshotRegionX
    {
        get => _screenshotRegionX;
        set => SetScriptField(ref _screenshotRegionX, value?.Trim() ?? string.Empty);
    }

    public string ScreenshotRegionY
    {
        get => _screenshotRegionY;
        set => SetScriptField(ref _screenshotRegionY, value?.Trim() ?? string.Empty);
    }

    public string ScreenshotRegionWidth
    {
        get => _screenshotRegionWidth;
        set => SetScriptField(ref _screenshotRegionWidth, value?.Trim() ?? string.Empty);
    }

    public string ScreenshotRegionHeight
    {
        get => _screenshotRegionHeight;
        set => SetScriptField(ref _screenshotRegionHeight, value?.Trim() ?? string.Empty);
    }

    public WindowCommandMode WindowCommandMode
    {
        get => _windowCommandMode;
        set => SetScriptField(ref _windowCommandMode, value);
    }

    public string WindowSelectorKind
    {
        get => _windowSelectorKind;
        set => SetScriptField(
            ref _windowSelectorKind,
            value?.Trim().ToUpperInvariant() switch
            {
                "ACTIVE" => "active",
                "TITLE" => "title",
                "CLASS" => "class",
                "ADDRESS" => "address",
                _ => value?.Trim() ?? string.Empty,
            });
    }

    public string WindowSelectorValue
    {
        get => _windowSelectorValue;
        set => SetScriptField(ref _windowSelectorValue, value ?? string.Empty);
    }

    public string WindowActiveField
    {
        get => _windowActiveField;
        set => SetScriptField(
            ref _windowActiveField,
            value?.Trim().ToUpperInvariant() switch
            {
                "TITLE" => "title",
                "CLASS" => "class",
                "ADDRESS" => "address",
                "FULLSCREEN" => "fullscreen",
                "MAXIMIZE" => "maximize",
                "FLOAT" => "float",
                "PINNED" => "pinned",
                "HIDDEN" => "hidden",
                "GEOMETRY" => "geometry",
                _ => value?.Trim() ?? string.Empty,
            });
    }

    public string WindowOutputVariable
    {
        get => _windowOutputVariable;
        set => SetScriptField(ref _windowOutputVariable, value?.Trim() ?? string.Empty);
    }

    public int WindowTimeoutMs
    {
        get => _windowTimeoutMs;
        set => SetScriptField(ref _windowTimeoutMs, value);
    }

    public int WindowX
    {
        get => _windowX;
        set => SetScriptField(ref _windowX, value);
    }

    public int WindowY
    {
        get => _windowY;
        set => SetScriptField(ref _windowY, value);
    }

    public int WindowWidth
    {
        get => _windowWidth;
        set => SetScriptField(ref _windowWidth, value);
    }

    public int WindowHeight
    {
        get => _windowHeight;
        set => SetScriptField(ref _windowHeight, value);
    }

    public string WindowWorkspace
    {
        get => _windowWorkspace;
        set => SetScriptField(ref _windowWorkspace, value ?? string.Empty);
    }

    public bool TryGetScreenReadingPayload(out EditorActionScreenReadingPayload payload)
    {
        return EditorActionScreenReadingPayload.TryCreate(this, out payload);
    }

    public void ApplyScreenReadingPayload(EditorActionScreenReadingPayload payload)
    {
        if (!EditorActionScreenReadingPayload.IsScreenReadingAction(payload.Type))
        {
            throw new ArgumentException("Payload type must be a screen-reading action.", nameof(payload));
        }

        Type = payload.Type;
        IsAbsolute = payload.IsAbsolute;
        ScreenX = payload.ScreenX;
        ScreenY = payload.ScreenY;
        ScreenLeft = payload.ScreenLeft;
        ScreenTop = payload.ScreenTop;
        ScreenWidth = payload.ScreenWidth;
        ScreenHeight = payload.ScreenHeight;
        ScreenColorHex = payload.ScreenColorHex;
        ScreenTargetColorSource = payload.ScreenTargetColorSource;
        ScreenTargetColorVariableName = payload.ScreenTargetColorVariableName;
        ScreenColorVariableName = payload.ScreenColorVariableName;
        ScreenTimeoutMs = payload.ScreenTimeoutMs;
        ScreenTolerance = payload.ScreenTolerance;
        ScreenFoundVariableName = payload.ScreenFoundVariableName;
        ScreenFoundXVariableName = payload.ScreenFoundXVariableName;
        ScreenFoundYVariableName = payload.ScreenFoundYVariableName;
        ImageAssetName = payload.ImageAssetName;
        ImageSearchSimilarity = payload.ImageSearchSimilarity;
        ImageSearchDownsample = payload.ImageSearchDownsample;
        ImageSearchScaleAware = payload.ImageSearchScaleAware;
        ImageSearchMatchMode = payload.ImageSearchMatchMode;
        ImageSearchMatchModeWasExplicit = payload.ImageSearchMatchModeWasExplicit;
        Button = payload.Button;
    }

    public bool TryGetScreenshotPayload(out EditorActionScreenshotPayload payload) =>
        EditorActionScreenshotPayload.TryCreate(this, out payload);

    public bool TryGetShellPayload(out EditorActionShellPayload payload) =>
        EditorActionShellPayload.TryCreate(this, out payload);

    public bool TryGetWindowPayload(out EditorActionWindowPayload payload) =>
        EditorActionWindowPayload.TryCreate(this, out payload);

    /// <summary>
    /// Gets a human-readable description of this action.
    /// </summary>
    public string DisplayName => GenerateDisplayName();

    private string GenerateDisplayName()
    {
        return Type switch
        {
            EditorActionType.MouseMove when IsAbsolute => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Move to ({X}, {Y})"),
            EditorActionType.MouseMove => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Move by ({X:+#;-#;0}, {Y:+#;-#;0})"),
            EditorActionType.MouseClick when UseCurrentPosition => $"Click {Button} at current position",
            EditorActionType.MouseClick when IsAbsolute => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Click {Button} at ({X}, {Y})"),
            EditorActionType.MouseClick => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Click {Button} by ({X:+#;-#;0}, {Y:+#;-#;0})"),
            EditorActionType.MouseDown when UseCurrentPosition => $"Hold {Button} at current position",
            EditorActionType.MouseDown when IsAbsolute => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Hold {Button} at ({X}, {Y})"),
            EditorActionType.MouseDown => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Hold {Button} by ({X:+#;-#;0}, {Y:+#;-#;0})"),
            EditorActionType.MouseUp when UseCurrentPosition => $"Release {Button} at current position",
            EditorActionType.MouseUp when IsAbsolute => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Release {Button} at ({X}, {Y})"),
            EditorActionType.MouseUp => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Release {Button} by ({X:+#;-#;0}, {Y:+#;-#;0})"),
            EditorActionType.KeyPress => $"Press '{KeyName ?? KeyCode.ToString(CultureInfo.CurrentCulture)}'",
            EditorActionType.KeyDown => $"Hold '{KeyName ?? KeyCode.ToString(CultureInfo.CurrentCulture)}'",
            EditorActionType.KeyUp => $"Release '{KeyName ?? KeyCode.ToString(CultureInfo.CurrentCulture)}'",
            EditorActionType.Delay when UseRandomDelay => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Wait {RandomDelayMinMs}-{RandomDelayMaxMs}ms (random)"),
            EditorActionType.Delay => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Wait {DelayMs}ms"),
            EditorActionType.ScrollVertical => ScrollAmount > 0 ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Scroll Up {ScrollAmount}") : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Scroll Down {Math.Abs(ScrollAmount)}"),
            EditorActionType.ScrollHorizontal => ScrollAmount > 0 ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Scroll Right {ScrollAmount}") : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Scroll Left {Math.Abs(ScrollAmount)}"),
            EditorActionType.TextInput => GetTextInputDisplayName(),
            EditorActionType.SetVariable => GetSetVariableDisplayName(),
            EditorActionType.IncrementVariable => GetIncrementVariableDisplayName(),
            EditorActionType.DecrementVariable => GetDecrementVariableDisplayName(),
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
            EditorActionType.PixelColor => BuildPixelColorDisplayName(ScreenReadingPayload),
            EditorActionType.WaitColor => BuildWaitColorDisplayName(ScreenReadingPayload),
            EditorActionType.PixelSearch => BuildPixelSearchDisplayName(ScreenReadingPayload),
            EditorActionType.ImageSearch => BuildImageSearchDisplayName(),
            EditorActionType.ImageClick => BuildImageClickDisplayName(),
            EditorActionType.WaitImage => BuildWaitImageDisplayName(),
            EditorActionType.ShellCommand => GetShellCommandDisplayName(),
            EditorActionType.Screenshot => BuildScreenshotDisplayName(),
            EditorActionType.WindowCommand => BuildWindowCommandDisplayName(),
            EditorActionType.Break => "Break",
            EditorActionType.Continue => "Continue",
            EditorActionType.BlockEnd => "End Block",
            EditorActionType.RawScriptStep => GetRawScriptStepDisplayName(),
            EditorActionType.ClipboardGet or EditorActionType.ClipboardSet => "Unknown Action",
            _ => "Unknown Action",
        };
    }

    private string GetTextInputDisplayName()
    {
        if (string.IsNullOrEmpty(Text))
        {
            return "Text Input (empty)";
        }
        var truncated = Text.Length > 25 ? Text[..25] + "..." : Text;
        return $"Type \"{truncated}\"";
    }

    private string GetSetVariableDisplayName()
    {
        if (UseLegacyScriptTextDisplay)
        {
            return $"Set {Text}";
        }
        return EditorActionScriptTokens.IsValidVariableName(ScriptVariableName)
            ? $"Set {ScriptVariableName} = {BuildSetValueToken()}"
            : "Set Variable";
    }

    private string GetIncrementVariableDisplayName()
    {
        if (UseLegacyScriptTextDisplay)
        {
            return $"Inc {Text}";
        }
        return EditorActionScriptTokens.IsValidVariableName(ScriptVariableName)
            ? $"Inc {ScriptVariableName} by {BuildNumericToken(ScriptNumericSourceType, ScriptNumericValue)}"
            : "Increment Variable";
    }

    private string GetDecrementVariableDisplayName()
    {
        if (UseLegacyScriptTextDisplay)
        {
            return $"Dec {Text}";
        }
        return EditorActionScriptTokens.IsValidVariableName(ScriptVariableName)
            ? $"Dec {ScriptVariableName} by {BuildNumericToken(ScriptNumericSourceType, ScriptNumericValue)}"
            : "Decrement Variable";
    }

    private string GetShellCommandDisplayName()
    {
        if (string.IsNullOrWhiteSpace(ShellCommand))
        {
            return "Shell Command";
        }
        var commandText = ShellCommand.Length > 30 ? ShellCommand[..30] + "..." : ShellCommand;
        return $"Shell {ShellCommandMode}: \"{commandText}\"";
    }

    private string GetRawScriptStepDisplayName()
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            return "Raw Script Step";
        }
        var stepText = Text.Length > 40 ? Text[..40] + "..." : Text;
        return $"Raw Script: {stepText}";
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
                && !(RandomDelayMinMs is 0 && RandomDelayMaxMs is 0),
            EditorActionType.Delay => DelayMs >= 0,
            EditorActionType.KeyPress or EditorActionType.KeyDown or EditorActionType.KeyUp => KeyCode > 0,
            EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal => ScrollAmount is not 0,
            EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp when UseCurrentPosition => !IsAbsolute,
            EditorActionType.TextInput => !string.IsNullOrEmpty(Text),
            EditorActionType.SetVariable => UseLegacyScriptTextDisplay || ValidateSetVariableFields(),
            EditorActionType.IncrementVariable or EditorActionType.DecrementVariable => UseLegacyScriptTextDisplay || ValidateIncDecFields(),
            EditorActionType.RepeatBlockStart => UseLegacyScriptTextDisplay || ValidateRepeatFields(),
            EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart => UseLegacyScriptTextDisplay || ValidateConditionFields(),
            EditorActionType.ForBlockStart => UseLegacyScriptTextDisplay || ValidateForFields(),
            EditorActionType.PixelColor => ValidatePixelColorFields(),
            EditorActionType.WaitColor => ValidateWaitColorFields(),
            EditorActionType.PixelSearch => ValidatePixelSearchFields(),
            EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage => ValidateImageSearchFields(),
            EditorActionType.ClipboardGet => EditorActionScriptTokens.IsValidVariableName(ScriptVariableName),
            EditorActionType.ClipboardSet => !string.IsNullOrEmpty(Text),
            EditorActionType.ShellCommand => ValidateShellCommandFields(),
            EditorActionType.Screenshot => ValidateScreenshotFields(),
            EditorActionType.WindowCommand => ValidateWindowCommandFields(),
            EditorActionType.RawScriptStep => !string.IsNullOrWhiteSpace(Text),
            EditorActionType.ElseBlockStart or EditorActionType.BlockEnd or EditorActionType.Break or EditorActionType.Continue => true,
            EditorActionType.MouseMove => true,
            _ => true,
        };
    }

    public EditorAction Clone()
    {
        var clone = new EditorAction
        {
            _id = Guid.NewGuid(), // New ID for clone
        };
        CopyInputFields(clone);
        CopyScriptFields(clone);
        CopyCommandFields(clone);
        clone.PreferLegacyScriptText = PreferLegacyScriptText;
        clone._preservedTextInputText = _preservedTextInputText;
        clone._preservedTextInputEvents = _preservedTextInputEvents?.ToList();

        if (TryGetScreenReadingPayload(out var screenReadingPayload))
        {
            clone.ApplyScreenReadingPayload(screenReadingPayload);
            clone.PreferLegacyScriptText = PreferLegacyScriptText;
        }
        else
        {
            clone._screenX = ScreenX;
            clone._screenY = ScreenY;
            clone._screenLeft = ScreenLeft;
            clone._screenTop = ScreenTop;
            clone._screenWidth = ScreenWidth;
            clone._screenHeight = ScreenHeight;
            clone._screenColorHex = ScreenColorHex;
            clone._screenTargetColorSource = ScreenTargetColorSource;
            clone._screenTargetColorVariableName = ScreenTargetColorVariableName;
            clone._screenColorVariableName = ScreenColorVariableName;
            clone._screenTimeoutMs = ScreenTimeoutMs;
            clone._screenTolerance = ScreenTolerance;
            clone._screenFoundVariableName = ScreenFoundVariableName;
            clone._screenFoundXVariableName = ScreenFoundXVariableName;
            clone._screenFoundYVariableName = ScreenFoundYVariableName;
            clone._imageAssetName = ImageAssetName;
            clone._imageSearchSimilarity = ImageSearchSimilarity;
            clone._imageSearchDownsample = ImageSearchDownsample;
        }

        return clone;
    }

    private void CopyInputFields(EditorAction clone)
    {
        clone._type = Type;
        clone._x = X;
        clone._y = Y;
        clone._isAbsolute = IsAbsolute;
        clone._button = Button;
        clone._keyCode = KeyCode;
        clone._delayMs = DelayMs;
        clone._useRandomDelay = UseRandomDelay;
        clone._randomDelayMinMs = RandomDelayMinMs;
        clone._randomDelayMaxMs = RandomDelayMaxMs;
        clone._useCurrentPosition = UseCurrentPosition;
        clone._scrollAmount = ScrollAmount;
        clone._keyName = KeyName;
        clone._text = Text;
    }

    private void CopyScriptFields(EditorAction clone)
    {
        clone._scriptVariableName = ScriptVariableName;
        clone._scriptValueType = ScriptValueType;
        clone._scriptValue = ScriptValue;
        clone._scriptNumericSourceType = ScriptNumericSourceType;
        clone._scriptNumericValue = ScriptNumericValue;
        clone._scriptLeftOperandType = ScriptLeftOperandType;
        clone._scriptLeftOperand = ScriptLeftOperand;
        clone._scriptConditionOperator = ScriptConditionOperator;
        clone._scriptRightOperandType = ScriptRightOperandType;
        clone._scriptRightOperand = ScriptRightOperand;
        clone._forVariableName = ForVariableName;
        clone._forStartType = ForStartType;
        clone._forStartValue = ForStartValue;
        clone._forEndType = ForEndType;
        clone._forEndValue = ForEndValue;
        clone._forHasStep = ForHasStep;
        clone._forStepType = ForStepType;
        clone._forStepValue = ForStepValue;
    }

    private void CopyCommandFields(EditorAction clone)
    {
        clone._imageAssetName = ImageAssetName;
        clone._imageSearchSimilarity = ImageSearchSimilarity;
        clone._imageSearchDownsample = ImageSearchDownsample;
        clone._shellCommandMode = ShellCommandMode;
        clone._shellCommand = ShellCommand;
        clone._shellStandardInput = ShellStandardInput;
        clone._shellExitCodeVariableName = ShellExitCodeVariableName;
        clone._shellStandardOutputVariableName = ShellStandardOutputVariableName;
        clone._shellStandardErrorVariableName = ShellStandardErrorVariableName;
        clone._shellRetries = ShellRetries;
        clone._shellBackoffMs = ShellBackoffMs;
        clone._shellTimeoutMs = ShellTimeoutMs;
        clone._screenshotOutputPath = ScreenshotOutputPath;
        clone._screenshotCopyToClipboard = ScreenshotCopyToClipboard;
        clone._screenshotUseRegion = ScreenshotUseRegion;
        clone._screenshotRegionX = ScreenshotRegionX;
        clone._screenshotRegionY = ScreenshotRegionY;
        clone._screenshotRegionWidth = ScreenshotRegionWidth;
        clone._screenshotRegionHeight = ScreenshotRegionHeight;
        clone._windowCommandMode = WindowCommandMode;
        clone._windowSelectorKind = WindowSelectorKind;
        clone._windowSelectorValue = WindowSelectorValue;
        clone._windowActiveField = WindowActiveField;
        clone._windowOutputVariable = WindowOutputVariable;
        clone._windowTimeoutMs = WindowTimeoutMs;
        clone._windowX = WindowX;
        clone._windowY = WindowY;
        clone._windowWidth = WindowWidth;
        clone._windowHeight = WindowHeight;
        clone._windowWorkspace = WindowWorkspace;
    }

    private bool UseLegacyScriptTextDisplay => PreferLegacyScriptText && !string.IsNullOrWhiteSpace(Text);

    private void ClearPreservedTextInputEvents()
    {
        _preservedTextInputEvents = null;
        _preservedTextInputText = null;
    }

    private void MarkStructuredScriptEdited()
    {
        if (IsScriptPayloadAction(Type))
        {
            PreferLegacyScriptText = false;
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
            or EditorActionType.ForBlockStart
            or EditorActionType.PixelColor
            or EditorActionType.WaitColor
            or EditorActionType.PixelSearch
            or EditorActionType.ImageSearch
            or EditorActionType.ImageClick
            or EditorActionType.WaitImage
            or EditorActionType.ClipboardGet
            or EditorActionType.ClipboardSet
            or EditorActionType.ShellCommand
            or EditorActionType.Screenshot
            or EditorActionType.WindowCommand;
    }

    private string BuildSetValueToken()
    {
        return EditorActionScriptTokens.FormatSetValueToken(ScriptValueType, ScriptValue);
    }

    private string BuildConditionPreview()
    {
        var left = BuildOperandToken(ScriptLeftOperandType, ScriptLeftOperand);
        var right = BuildOperandToken(ScriptRightOperandType, ScriptRightOperand);
        return $"{left} {EditorActionScriptTokens.ToOperatorToken(ScriptConditionOperator)} {right}";
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
        return EditorActionScriptTokens.FormatNumericToken(sourceType, value);
    }

    private static string BuildOperandToken(ScriptOperandType operandType, string value)
    {
        return EditorActionScriptTokens.FormatOperandToken(operandType, value);
    }

    private bool ValidateSetVariableFields()
    {
        if (!EditorActionScriptTokens.IsValidVariableName(ScriptVariableName))
        {
            return false;
        }

        return ScriptValueType switch
        {
            ScriptValueType.Number => int.TryParse(ScriptValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _),
            ScriptValueType.Boolean => bool.TryParse(ScriptValue, out _),
            ScriptValueType.Text => !string.IsNullOrWhiteSpace(ScriptValue),
            ScriptValueType.VariableReference => EditorActionScriptTokens.IsValidVariableName(ScriptValue),
            _ => false,
        };
    }

    private bool ValidateIncDecFields()
    {
        return EditorActionScriptTokens.IsValidVariableName(ScriptVariableName)
            && EditorActionScriptTokens.ValidateNumericToken(ScriptNumericSourceType, ScriptNumericValue);
    }

    private bool ValidateRepeatFields()
    {
        return EditorActionScriptTokens.ValidateNumericToken(ScriptNumericSourceType, ScriptNumericValue);
    }

    private bool ValidateConditionFields()
    {
        return EditorActionScriptTokens.ValidateOperandToken(ScriptLeftOperandType, ScriptLeftOperand)
            && EditorActionScriptTokens.ValidateOperandToken(ScriptRightOperandType, ScriptRightOperand);
    }

    private bool ValidateForFields()
    {
        if (!EditorActionScriptTokens.IsValidVariableName(ForVariableName))
        {
            return false;
        }

        if (!EditorActionScriptTokens.ValidateNumericToken(ForStartType, ForStartValue)
            || !EditorActionScriptTokens.ValidateNumericToken(ForEndType, ForEndValue))
        {
            return false;
        }

        return !ForHasStep || EditorActionScriptTokens.ValidateNumericToken(ForStepType, ForStepValue);
    }

    private bool ValidatePixelColorFields()
    {
        return TryGetScreenReadingPayload(out var payload) && payload.HasValidColorVariableName();
    }

    private bool ValidateWaitColorFields()
    {
        return TryGetScreenReadingPayload(out var payload)
            && payload.HasValidTargetColor()
            && payload.ScreenTimeoutMs >= 0;
    }

    private bool ValidatePixelSearchFields()
    {
        return TryGetScreenReadingPayload(out var payload)
            && payload.HasValidTargetColor()
            && payload.HasPositiveSearchRegion()
            && payload.HasValidTolerance()
            && payload.HasValidFoundCoordinateVariableNames();
    }

    private bool ValidateImageSearchFields()
    {
        return EditorActionScriptTokens.IsValidVariableName(ImageAssetName)
            && ScreenWidth > 0
            && ScreenHeight > 0
            && EditorActionScriptTokens.IsValidVariableName(ScreenFoundVariableName)
            && EditorActionScriptTokens.IsValidVariableName(ScreenFoundXVariableName)
            && EditorActionScriptTokens.IsValidVariableName(ScreenFoundYVariableName)
            && double.IsFinite(ImageSearchSimilarity)
            && ImageSearchSimilarity is >= 0.0 and <= 1.0
            && ImageSearchDownsample >= 1
            && (Type is not EditorActionType.ImageClick
                || Button is MacroMouseButton.Left or MacroMouseButton.Right or MacroMouseButton.Middle);
    }

    private bool ValidateShellCommandFields()
    {
        if (string.IsNullOrWhiteSpace(ShellCommand) || ShellRetries < 0 || ShellRetries > 10_000 || ShellBackoffMs < 0 || ShellTimeoutMs < 0)
        {
            return false;
        }

        if (ShellCommandMode is ShellCommandMode.ShellCapture or ShellCommandMode.ShellCaptureInput)
        {
            return IsValidShellCaptureTarget(ShellExitCodeVariableName)
                && IsValidShellCaptureTarget(ShellStandardOutputVariableName)
                && IsValidShellCaptureTarget(ShellStandardErrorVariableName);
        }

        return true;
    }

    private bool ValidateScreenshotFields()
    {
        if (string.IsNullOrWhiteSpace(ScreenshotOutputPath) && !ScreenshotCopyToClipboard)
        {
            return false;
        }

        return !ScreenshotUseRegion || (IsIntegerOrVariable(ScreenshotRegionX)
            && IsIntegerOrVariable(ScreenshotRegionY)
            && IsPositiveIntegerOrVariable(ScreenshotRegionWidth)
            && IsPositiveIntegerOrVariable(ScreenshotRegionHeight));
    }

    private bool ValidateWindowCommandFields()
    {
        return WindowCommandMode switch
        {
            WindowCommandMode.Active => IsValidWindowActiveField(WindowActiveField)
                && EditorActionScriptTokens.IsValidVariableName(WindowOutputVariable),
            WindowCommandMode.Search => IsValidWindowSearchSelector(WindowSelectorKind)
                && !string.IsNullOrWhiteSpace(WindowSelectorValue)
                && EditorActionScriptTokens.IsValidVariableName(WindowOutputVariable),
            WindowCommandMode.Wait => IsValidWindowSearchSelector(WindowSelectorKind)
                && !string.IsNullOrWhiteSpace(WindowSelectorValue)
                && WindowTimeoutMs > 0
                && EditorActionScriptTokens.IsValidVariableName(WindowOutputVariable),
            WindowCommandMode.Focus => string.Equals(WindowSelectorKind, "active", StringComparison.Ordinal)
                || (IsValidWindowFocusSelector(WindowSelectorKind) && !string.IsNullOrWhiteSpace(WindowSelectorValue)),
            WindowCommandMode.Close => string.Equals(WindowSelectorKind, "active", StringComparison.Ordinal)
                || (IsValidWindowCloseSelector(WindowSelectorKind) && !string.IsNullOrWhiteSpace(WindowSelectorValue)),
            WindowCommandMode.Resize => WindowWidth > 0 && WindowHeight > 0,
            WindowCommandMode.WorkspaceGet => EditorActionScriptTokens.IsValidVariableName(WindowOutputVariable),
            WindowCommandMode.WorkspaceSwitch or WindowCommandMode.WorkspaceMoveActive => !string.IsNullOrWhiteSpace(WindowWorkspace),
            WindowCommandMode.WorkspaceMoveWindow => !string.IsNullOrWhiteSpace(WindowSelectorValue) && !string.IsNullOrWhiteSpace(WindowWorkspace),
            WindowCommandMode.Move
                or WindowCommandMode.Center
                or WindowCommandMode.Maximize
                or WindowCommandMode.Fullscreen
                or WindowCommandMode.Floating => true,
            _ => true,
        };
    }

    private static bool IsIntegerOrVariable(string token)
    {
        return int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)
            || (token.StartsWith('$') && EditorActionScriptTokens.IsValidVariableName(token));
    }

    private static bool IsPositiveIntegerOrVariable(string token)
    {
        return int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value > 0
            : token.StartsWith('$') && EditorActionScriptTokens.IsValidVariableName(token);
    }

    private static bool IsValidShellCaptureTarget(string target)
    {
        return string.Equals(target, "_", StringComparison.Ordinal) || EditorActionScriptTokens.IsValidVariableName(target);
    }

    private EditorActionScreenReadingPayload ScreenReadingPayload
    {
        get
        {
            if (!TryGetScreenReadingPayload(out var payload))
            {
                throw new InvalidOperationException("Action type does not contain a screen-reading payload.");
            }

            return payload;
        }
    }

    private static string BuildPixelColorDisplayName(EditorActionScreenReadingPayload payload)
    {
        return payload.IsAbsolute
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Pixel color ({payload.ScreenX}, {payload.ScreenY}) -> {payload.ScreenColorVariableName}")
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Pixel color rel ({payload.ScreenX:+#;-#;0}, {payload.ScreenY:+#;-#;0}) -> {payload.ScreenColorVariableName}");
    }

    private static string BuildWaitColorDisplayName(EditorActionScreenReadingPayload payload)
    {
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Wait color {payload.FormatTargetColorToken()} at ({payload.ScreenX}, {payload.ScreenY}) -> {payload.ScreenColorVariableName}");
    }

    private static string BuildPixelSearchDisplayName(EditorActionScreenReadingPayload payload)
    {
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Pixel search {payload.FormatTargetColorToken()} in ({payload.ScreenLeft}, {payload.ScreenTop}, {payload.ScreenWidth}x{payload.ScreenHeight}) -> {payload.ScreenFoundVariableName}, {payload.ScreenFoundXVariableName}, {payload.ScreenFoundYVariableName}");
    }

    private string BuildImageSearchDisplayName()
    {
        var imageName = string.IsNullOrWhiteSpace(ImageAssetName) ? "image required" : ImageAssetName;
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Image search {imageName} in ({ScreenLeft}, {ScreenTop}, {ScreenWidth}x{ScreenHeight}) -> {ScreenFoundVariableName}, {ScreenFoundXVariableName}, {ScreenFoundYVariableName}");
    }

    private string BuildImageClickDisplayName()
    {
        var imageName = string.IsNullOrWhiteSpace(ImageAssetName) ? "image required" : ImageAssetName;
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Image click {imageName} in ({ScreenLeft}, {ScreenTop}, {ScreenWidth}x{ScreenHeight})");
    }

    private string BuildWaitImageDisplayName()
    {
        var imageName = string.IsNullOrWhiteSpace(ImageAssetName) ? "image required" : ImageAssetName;
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Wait image {imageName} ({ScreenTimeoutMs}ms) -> {ScreenFoundVariableName}, {ScreenFoundXVariableName}, {ScreenFoundYVariableName}");
    }

    private string BuildScreenshotDisplayName()
    {
        string destination;
        if (ScreenshotCopyToClipboard)
        {
            destination = string.IsNullOrWhiteSpace(ScreenshotOutputPath) ? "clipboard" : $"{ScreenshotOutputPath} + clipboard";
        }
        else
        {
            destination = string.IsNullOrWhiteSpace(ScreenshotOutputPath) ? "destination required" : ScreenshotOutputPath;
        }

        return ScreenshotUseRegion
            ? $"Screenshot ({ScreenshotRegionX}, {ScreenshotRegionY}, {ScreenshotRegionWidth}x{ScreenshotRegionHeight}) -> {destination}"
            : $"Screenshot -> {destination}";
    }

    private string BuildWindowCommandDisplayName()
    {
        return WindowCommandMode switch
        {
            WindowCommandMode.Active => $"Get active window {WindowActiveField} -> {WindowOutputVariable}",
            WindowCommandMode.Search => $"Search window by {WindowSelectorKind} \"{WindowSelectorValue}\" -> {WindowOutputVariable}",
            WindowCommandMode.Wait => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Wait for window {WindowSelectorKind} \"{WindowSelectorValue}\" ({WindowTimeoutMs}ms) -> {WindowOutputVariable}"),
            WindowCommandMode.Focus => FormatWindowSelectorSummary("Focus"),
            WindowCommandMode.Close => FormatWindowSelectorSummary("Close"),
            WindowCommandMode.Move => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Move active window to {WindowX}, {WindowY}"),
            WindowCommandMode.Resize => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Resize active window to {WindowWidth}x{WindowHeight}"),
            WindowCommandMode.Center => "Center active window",
            WindowCommandMode.Maximize => "Maximize active window",
            WindowCommandMode.Fullscreen => "Fullscreen active window",
            WindowCommandMode.Floating => "Float active window",
            WindowCommandMode.WorkspaceGet => $"Get active workspace -> {WindowOutputVariable}",
            WindowCommandMode.WorkspaceSwitch => $"Switch to workspace {WindowWorkspace}",
            WindowCommandMode.WorkspaceMoveActive => $"Move active window to workspace {WindowWorkspace}",
            WindowCommandMode.WorkspaceMoveWindow => $"Move window {WindowSelectorValue} to workspace {WindowWorkspace}",
            _ => "Window Command",
        };
    }

    private string FormatWindowSelectorSummary(string verb)
    {
        return string.Equals(WindowSelectorKind, "active", StringComparison.Ordinal)
            ? $"{verb} active window"
            : $"{verb} window by {WindowSelectorKind} \"{WindowSelectorValue}\"";
    }

    private static bool IsValidWindowActiveField(string value)
    {
        return string.Equals(value, "title", StringComparison.Ordinal)
            || string.Equals(value, "class", StringComparison.Ordinal)
            || string.Equals(value, "address", StringComparison.Ordinal)
            || string.Equals(value, "fullscreen", StringComparison.Ordinal)
            || string.Equals(value, "maximize", StringComparison.Ordinal)
            || string.Equals(value, "float", StringComparison.Ordinal)
            || string.Equals(value, "pinned", StringComparison.Ordinal)
            || string.Equals(value, "hidden", StringComparison.Ordinal)
            || string.Equals(value, "geometry", StringComparison.Ordinal);
    }

    private static bool IsValidWindowSearchSelector(string value)
    {
        return string.Equals(value, "title", StringComparison.Ordinal)
            || string.Equals(value, "class", StringComparison.Ordinal);
    }

    private static bool IsValidWindowFocusSelector(string value)
    {
        return string.Equals(value, "title", StringComparison.Ordinal)
            || string.Equals(value, "class", StringComparison.Ordinal)
            || string.Equals(value, "address", StringComparison.Ordinal);
    }

    private static bool IsValidWindowCloseSelector(string value)
    {
        return string.Equals(value, "title", StringComparison.Ordinal)
            || string.Equals(value, "address", StringComparison.Ordinal);
    }

    private void SetScreenField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        SetScriptField(ref field, value, propertyName);
    }

    private void SetScriptField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        MarkStructuredScriptEdited();
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(DisplayName));
    }

    private static string NormalizeColorHex(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

}
