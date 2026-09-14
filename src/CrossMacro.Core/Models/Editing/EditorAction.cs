
namespace CrossMacro.Core.Models.Editing;

/// <summary>
/// Represents a single action in the macro editor.
/// Provides a user-friendly abstraction over MacroEvent for editing.
/// Implements INotifyPropertyChanged for proper UI binding.
/// </summary>
public partial class EditorAction : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    private EditorActionType _type;
    private InputState _input = new();
    private ScriptState _script = new();
    private ScreenState _screen = new();
    private ShellState _shell = new();
    private ScreenshotState _screenshot = new();
    private WindowState _window = new();

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

                if (!EditorActionValidationPolicy.IsScriptPayloadAction(value))
                {
                    PreferLegacyScriptText = false;
                }
                else if (!string.IsNullOrWhiteSpace(_input.Text))
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
        get => _input.X;
        set
        {
            if (_input.X != value || _input.CoordinateXToken is not null)
            {
                _input.X = value;
                _input.CoordinateXToken = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CoordinateXToken));
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
        get => _input.Y;
        set
        {
            if (_input.Y != value || _input.CoordinateYToken is not null)
            {
                _input.Y = value;
                _input.CoordinateYToken = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CoordinateYToken));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// X coordinate token used by script-backed mouse actions.
    /// Accepts an integer literal or a variable reference such as <c>$found_x</c>.
    /// </summary>
    public string CoordinateXToken
    {
        get => _input.CoordinateXToken ?? _input.X.ToString(CultureInfo.InvariantCulture);
        set => SetCoordinateToken(
            value,
            ref _input.CoordinateXToken,
            ref _input.X,
            nameof(CoordinateXToken),
            nameof(X));
    }

    /// <summary>
    /// Y coordinate token used by script-backed mouse actions.
    /// Accepts an integer literal or a variable reference such as <c>$found_y</c>.
    /// </summary>
    public string CoordinateYToken
    {
        get => _input.CoordinateYToken ?? _input.Y.ToString(CultureInfo.InvariantCulture);
        set => SetCoordinateToken(
            value,
            ref _input.CoordinateYToken,
            ref _input.Y,
            nameof(CoordinateYToken),
            nameof(Y));
    }

    public bool HasVariableCoordinates =>
        IsVariableCoordinateToken(CoordinateXToken)
        || IsVariableCoordinateToken(CoordinateYToken);

    public bool TryGetLiteralCoordinates(out int x, out int y)
    {
        var hasX = int.TryParse(CoordinateXToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out x);
        var hasY = int.TryParse(CoordinateYToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
        return hasX && hasY;
    }

    /// <summary>
    /// Whether coordinates are absolute (true) or relative (false).
    /// Used by mouse actions with coordinates (MouseMove/MouseClick/MouseDown/MouseUp).
    /// </summary>
    public bool IsAbsolute
    {
        get => _input.IsAbsolute;
        set
        {
            if (_input.IsAbsolute != value)
            {
                _input.IsAbsolute = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Unit space for relative mouse coordinates. Newly authored relative editor
    /// actions use direct raw input deltas; logical desktop pixels are opt-in.
    /// </summary>
    public MouseCoordinateSpace CoordinateSpace
    {
        get => _input.CoordinateSpace;
        set
        {
            if (_input.CoordinateSpace != value)
            {
                _input.CoordinateSpace = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Mouse button (for click/down/up actions).
    /// </summary>
    public MacroMouseButton Button
    {
        get => _input.Button;
        set
        {
            if (_input.Button != value)
            {
                _input.Button = value;
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
        get => _input.KeyCode;
        set
        {
            if (_input.KeyCode != value)
            {
                _input.KeyCode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>Precise delay in microseconds.</summary>
    public long DelayMicroseconds
    {
        get => _input.DelayMicroseconds;
        set
        {
            if (_input.DelayMicroseconds != value)
            {
                _input.DelayMicroseconds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DelayMs));
                OnPropertyChanged(nameof(DelayDuration));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>User-facing delay text accepting legacy milliseconds or explicit ms/us units.</summary>
    public string DelayDuration
    {
        get => DelayMicroseconds < 0
            ? string.Create(CultureInfo.InvariantCulture, $"{DelayMicroseconds}us")
            : MacroTiming.FormatDuration(DelayMicroseconds);
        set
        {
            if (MacroTiming.TryParseDurationMicroseconds(value, out var microseconds))
            {
                DelayMicroseconds = microseconds;
            }
        }
    }

    /// <summary>Legacy millisecond projection of <see cref="DelayMicroseconds"/>.</summary>
    public int DelayMs
    {
        get => MacroTiming.ToLegacyMilliseconds(DelayMicroseconds);
        set => DelayMicroseconds = checked((long)value * MacroTiming.MicrosecondsPerMillisecond);
    }

    /// <summary>
    /// Whether delay should be randomized between min/max bounds.
    /// Only applicable for Delay action.
    /// </summary>
    public bool UseRandomDelay
    {
        get => _input.UseRandomDelay;
        set
        {
            if (_input.UseRandomDelay != value)
            {
                _input.UseRandomDelay = value;
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
        get => _input.UseCurrentPosition;
        set
        {
            if (_input.UseCurrentPosition != value)
            {
                _input.UseCurrentPosition = value;
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
        get => _input.RandomDelayMinMs;
        set
        {
            if (_input.RandomDelayMinMs != value)
            {
                _input.RandomDelayMinMs = value;
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
        get => _input.RandomDelayMaxMs;
        set
        {
            if (_input.RandomDelayMaxMs != value)
            {
                _input.RandomDelayMaxMs = value;
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
        get => _input.ScrollAmount;
        set
        {
            if (_input.ScrollAmount != value)
            {
                _input.ScrollAmount = value;
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
        get => _input.KeyName;
        set
        {
            if (!string.Equals(_input.KeyName, value, StringComparison.Ordinal))
            {
                _input.KeyName = value;
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
        get => _input.Text;
        set
        {
            if (!string.Equals(_input.Text, value, StringComparison.Ordinal))
            {
                var normalized = value ?? string.Empty;
                _input.Text = normalized;
                if (Type is EditorActionType.TextInput && !string.Equals(_input.PreservedTextInputText, normalized, StringComparison.Ordinal))
                {
                    ClearPreservedTextInputEvents();
                }

                if (EditorActionValidationPolicy.IsScriptPayloadAction(Type))
                {
                    PreferLegacyScriptText = !string.IsNullOrWhiteSpace(_input.Text);
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
    public bool PreferLegacyScriptText { get => _script.PreferLegacyScriptText; set => _script.PreferLegacyScriptText = value; }

    public IReadOnlyList<MacroEvent>? GetPreservedTextInputEvents()
    {
        return Type is EditorActionType.TextInput
&& _input.PreservedTextInputEvents is { Count: > 0 }
&& string.Equals(_input.PreservedTextInputText, Text
, StringComparison.Ordinal) ? _input.PreservedTextInputEvents
            : null;
    }

    public void PreserveTextInputEvents(IEnumerable<MacroEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _input.PreservedTextInputEvents = events.ToList();
        _input.PreservedTextInputText = Text;
    }

    /// <summary>
    /// Variable name used by Set/Inc/Dec actions.
    /// </summary>
    public string ScriptVariableName
    {
        get => _script.ScriptVariableName;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_script.ScriptVariableName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _script.ScriptVariableName = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Shortcut used by <see cref="EditorActionType.CopySelectionToVariable"/>.
    /// </summary>
    public ClipboardCopyShortcut ClipboardCopyShortcut
    {
        get => _script.ClipboardCopyShortcut;
        set
        {
            if (_script.ClipboardCopyShortcut == value)
            {
                return;
            }

            _script.ClipboardCopyShortcut = value;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Variable name that receives the current logical cursor X coordinate.
    /// </summary>
    public string MousePositionXVariableName
    {
        get => _script.MousePositionXVariableName;
        set => SetMousePositionVariableName(ref _script.MousePositionXVariableName, value, nameof(MousePositionXVariableName));
    }

    /// <summary>
    /// Variable name that receives the current logical cursor Y coordinate.
    /// </summary>
    public string MousePositionYVariableName
    {
        get => _script.MousePositionYVariableName;
        set => SetMousePositionVariableName(ref _script.MousePositionYVariableName, value, nameof(MousePositionYVariableName));
    }

    /// <summary>
    /// Value kind for SetVariable action.
    /// </summary>
    public ScriptValueType ScriptValueType
    {
        get => _script.ScriptValueType;
        set
        {
            if (_script.ScriptValueType == value)
            {
                return;
            }

            _script.ScriptValueType = value;
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
        get => _script.ScriptValue;
        set
        {
            var normalized = value ?? string.Empty;
            if (string.Equals(_script.ScriptValue, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _script.ScriptValue = normalized;
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
        get => _script.ScriptNumericSourceType;
        set
        {
            if (_script.ScriptNumericSourceType == value)
            {
                return;
            }

            _script.ScriptNumericSourceType = value;
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
        get => _script.ScriptNumericValue;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_script.ScriptNumericValue, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _script.ScriptNumericValue = normalized;
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
        get => _script.ScriptLeftOperandType;
        set
        {
            if (_script.ScriptLeftOperandType == value)
            {
                return;
            }

            _script.ScriptLeftOperandType = value;
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
        get => _script.ScriptLeftOperand;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_script.ScriptLeftOperand, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _script.ScriptLeftOperand = normalized;
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
        get => _script.ScriptConditionOperator;
        set
        {
            if (_script.ScriptConditionOperator == value)
            {
                return;
            }

            _script.ScriptConditionOperator = value;
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
        get => _script.ScriptRightOperandType;
        set
        {
            if (_script.ScriptRightOperandType == value)
            {
                return;
            }

            _script.ScriptRightOperandType = value;
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
        get => _script.ScriptRightOperand;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_script.ScriptRightOperand, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _script.ScriptRightOperand = normalized;
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
        get => _script.ForVariableName;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_script.ForVariableName, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _script.ForVariableName = normalized;
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
        get => _script.ForStartType;
        set
        {
            if (_script.ForStartType == value)
            {
                return;
            }

            _script.ForStartType = value;
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
        get => _script.ForStartValue;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_script.ForStartValue, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _script.ForStartValue = normalized;
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
        get => _script.ForEndType;
        set
        {
            if (_script.ForEndType == value)
            {
                return;
            }

            _script.ForEndType = value;
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
        get => _script.ForEndValue;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_script.ForEndValue, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _script.ForEndValue = normalized;
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
        get => _script.ForHasStep;
        set
        {
            if (_script.ForHasStep == value)
            {
                return;
            }

            _script.ForHasStep = value;
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
        get => _script.ForStepType;
        set
        {
            if (_script.ForStepType == value)
            {
                return;
            }

            _script.ForStepType = value;
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
        get => _script.ForStepValue;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(_script.ForStepValue, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _script.ForStepValue = normalized;
            MarkStructuredScriptEdited();
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public int ScreenX
    {
        get => _screen.ScreenX;
        set => SetScreenField(ref _screen.ScreenX, value);
    }

    public int ScreenY
    {
        get => _screen.ScreenY;
        set => SetScreenField(ref _screen.ScreenY, value);
    }

    public int ScreenLeft
    {
        get => _screen.ScreenLeft;
        set => SetImageSearchRegionLiteral(
            value,
            ref _screen.ScreenLeft,
            ref _screen.ImageSearchRegionLeftToken,
            nameof(ScreenLeft),
            nameof(ImageSearchRegionLeftToken));
    }

    public int ScreenTop
    {
        get => _screen.ScreenTop;
        set => SetImageSearchRegionLiteral(
            value,
            ref _screen.ScreenTop,
            ref _screen.ImageSearchRegionTopToken,
            nameof(ScreenTop),
            nameof(ImageSearchRegionTopToken));
    }

    public int ScreenWidth
    {
        get => _screen.ScreenWidth;
        set => SetImageSearchRegionLiteral(
            value,
            ref _screen.ScreenWidth,
            ref _screen.ImageSearchRegionWidthToken,
            nameof(ScreenWidth),
            nameof(ImageSearchRegionWidthToken));
    }

    public int ScreenHeight
    {
        get => _screen.ScreenHeight;
        set => SetImageSearchRegionLiteral(
            value,
            ref _screen.ScreenHeight,
            ref _screen.ImageSearchRegionHeightToken,
            nameof(ScreenHeight),
            nameof(ImageSearchRegionHeightToken));
    }

    /// <summary>
    /// Left edge token for image search actions. Accepts an integer literal or a variable reference.
    /// </summary>
    public string ImageSearchRegionLeftToken
    {
        get => _screen.ImageSearchRegionLeftToken ?? ScreenLeft.ToString(CultureInfo.InvariantCulture);
        set => SetImageSearchRegionToken(
            value,
            ref _screen.ImageSearchRegionLeftToken,
            ref _screen.ScreenLeft,
            nameof(ImageSearchRegionLeftToken),
            nameof(ScreenLeft));
    }

    /// <summary>
    /// Top edge token for image search actions. Accepts an integer literal or a variable reference.
    /// </summary>
    public string ImageSearchRegionTopToken
    {
        get => _screen.ImageSearchRegionTopToken ?? ScreenTop.ToString(CultureInfo.InvariantCulture);
        set => SetImageSearchRegionToken(
            value,
            ref _screen.ImageSearchRegionTopToken,
            ref _screen.ScreenTop,
            nameof(ImageSearchRegionTopToken),
            nameof(ScreenTop));
    }

    /// <summary>
    /// Width token for image search actions. Accepts a positive integer literal or a variable reference.
    /// </summary>
    public string ImageSearchRegionWidthToken
    {
        get => _screen.ImageSearchRegionWidthToken ?? ScreenWidth.ToString(CultureInfo.InvariantCulture);
        set => SetImageSearchRegionToken(
            value,
            ref _screen.ImageSearchRegionWidthToken,
            ref _screen.ScreenWidth,
            nameof(ImageSearchRegionWidthToken),
            nameof(ScreenWidth));
    }

    /// <summary>
    /// Height token for image search actions. Accepts a positive integer literal or a variable reference.
    /// </summary>
    public string ImageSearchRegionHeightToken
    {
        get => _screen.ImageSearchRegionHeightToken ?? ScreenHeight.ToString(CultureInfo.InvariantCulture);
        set => SetImageSearchRegionToken(
            value,
            ref _screen.ImageSearchRegionHeightToken,
            ref _screen.ScreenHeight,
            nameof(ImageSearchRegionHeightToken),
            nameof(ScreenHeight));
    }

    public bool TryGetLiteralImageSearchRegion(out int left, out int top, out int width, out int height)
    {
        var hasLeft = int.TryParse(ImageSearchRegionLeftToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out left);
        var hasTop = int.TryParse(ImageSearchRegionTopToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out top);
        var hasWidth = int.TryParse(ImageSearchRegionWidthToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out width);
        var hasHeight = int.TryParse(ImageSearchRegionHeightToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
        return hasLeft && hasTop && hasWidth && hasHeight;
    }

    public bool HasValidImageSearchRegionTokens()
    {
        return IsValidImageSearchRegionToken(ImageSearchRegionLeftToken, mustBePositive: false)
            && IsValidImageSearchRegionToken(ImageSearchRegionTopToken, mustBePositive: false)
            && IsValidImageSearchRegionToken(ImageSearchRegionWidthToken, mustBePositive: true)
            && IsValidImageSearchRegionToken(ImageSearchRegionHeightToken, mustBePositive: true);
    }

    public string ScreenColorHex
    {
        get => _screen.ScreenColorHex;
        set => SetScreenField(ref _screen.ScreenColorHex, NormalizeColorHex(value));
    }

    public EditorActionScreenTargetColorSource ScreenTargetColorSource
    {
        get => _screen.ScreenTargetColorSource;
        set => SetScreenField(ref _screen.ScreenTargetColorSource, value);
    }

    public string ScreenTargetColorVariableName
    {
        get => _screen.ScreenTargetColorVariableName;
        set => SetScreenField(ref _screen.ScreenTargetColorVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenColorVariableName
    {
        get => _screen.ScreenColorVariableName;
        set => SetScreenField(ref _screen.ScreenColorVariableName, value?.Trim() ?? string.Empty);
    }

    public int ScreenTimeoutMs
    {
        get => _screen.ScreenTimeoutMs;
        set => SetScreenField(ref _screen.ScreenTimeoutMs, value);
    }

    public int ScreenTolerance
    {
        get => _screen.ScreenTolerance;
        set => SetScreenField(ref _screen.ScreenTolerance, value);
    }

    public string ScreenFoundVariableName
    {
        get => _screen.ScreenFoundVariableName;
        set => SetScreenField(ref _screen.ScreenFoundVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenFoundXVariableName
    {
        get => _screen.ScreenFoundXVariableName;
        set => SetScreenField(ref _screen.ScreenFoundXVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenFoundYVariableName
    {
        get => _screen.ScreenFoundYVariableName;
        set => SetScreenField(ref _screen.ScreenFoundYVariableName, value?.Trim() ?? string.Empty);
    }

    public string ImageAssetName
    {
        get => _screen.ImageAssetName;
        set => SetScreenField(ref _screen.ImageAssetName, value?.Trim() ?? string.Empty);
    }

    public double ImageSearchSimilarity
    {
        get => _screen.ImageSearchSimilarity;
        set => SetScreenField(ref _screen.ImageSearchSimilarity, value);
    }

    public EditorImageMatchMode ImageSearchMatchMode
    {
        get => _screen.ImageSearchMatchMode;
        set => SetScreenField(ref _screen.ImageSearchMatchMode, value);
    }

    public bool ImageSearchMatchModeWasExplicit { get => _screen.ImageSearchMatchModeWasExplicit; set => _screen.ImageSearchMatchModeWasExplicit = value; }

    public void SetImageSearchMatchMode(EditorImageMatchMode value, bool wasExplicit = true)
    {
        ImageSearchMatchMode = value;
        ImageSearchMatchModeWasExplicit = wasExplicit;
        MarkStructuredScriptEdited();
    }

    public ShellCommandMode ShellCommandMode
    {
        get => _shell.ShellCommandMode;
        set => SetScriptField(ref _shell.ShellCommandMode, value);
    }

    public string ShellCommand
    {
        get => _shell.ShellCommand;
        set => SetScriptField(ref _shell.ShellCommand, value ?? string.Empty);
    }

    public string ShellStandardInput
    {
        get => _shell.ShellStandardInput;
        set => SetScriptField(ref _shell.ShellStandardInput, value ?? string.Empty);
    }

    public string ShellExitCodeVariableName
    {
        get => _shell.ShellExitCodeVariableName;
        set => SetScriptField(ref _shell.ShellExitCodeVariableName, value?.Trim() ?? string.Empty);
    }

    public string ShellStandardOutputVariableName
    {
        get => _shell.ShellStandardOutputVariableName;
        set => SetScriptField(ref _shell.ShellStandardOutputVariableName, value?.Trim() ?? string.Empty);
    }

    public string ShellStandardErrorVariableName
    {
        get => _shell.ShellStandardErrorVariableName;
        set => SetScriptField(ref _shell.ShellStandardErrorVariableName, value?.Trim() ?? string.Empty);
    }

    public int ShellRetries
    {
        get => _shell.ShellRetries;
        set => SetScriptField(ref _shell.ShellRetries, value);
    }

    public int ShellBackoffMs
    {
        get => _shell.ShellBackoffMs;
        set => SetScriptField(ref _shell.ShellBackoffMs, value);
    }

    public int ShellTimeoutMs
    {
        get => _shell.ShellTimeoutMs;
        set => SetScriptField(ref _shell.ShellTimeoutMs, value);
    }

    public string ScreenshotOutputPath
    {
        get => _screenshot.ScreenshotOutputPath;
        set => SetScriptField(ref _screenshot.ScreenshotOutputPath, value ?? string.Empty);
    }

    public bool ScreenshotCopyToClipboard
    {
        get => _screenshot.ScreenshotCopyToClipboard;
        set => SetScriptField(ref _screenshot.ScreenshotCopyToClipboard, value);
    }

    public bool ScreenshotUseRegion
    {
        get => _screenshot.ScreenshotUseRegion;
        set => SetScriptField(ref _screenshot.ScreenshotUseRegion, value);
    }

    public string ScreenshotRegionX
    {
        get => _screenshot.ScreenshotRegionX;
        set => SetScriptField(ref _screenshot.ScreenshotRegionX, value?.Trim() ?? string.Empty);
    }

    public string ScreenshotRegionY
    {
        get => _screenshot.ScreenshotRegionY;
        set => SetScriptField(ref _screenshot.ScreenshotRegionY, value?.Trim() ?? string.Empty);
    }

    public string ScreenshotRegionWidth
    {
        get => _screenshot.ScreenshotRegionWidth;
        set => SetScriptField(ref _screenshot.ScreenshotRegionWidth, value?.Trim() ?? string.Empty);
    }

    public string ScreenshotRegionHeight
    {
        get => _screenshot.ScreenshotRegionHeight;
        set => SetScriptField(ref _screenshot.ScreenshotRegionHeight, value?.Trim() ?? string.Empty);
    }

    public WindowCommandMode WindowCommandMode
    {
        get => _window.WindowCommandMode;
        set => SetScriptField(ref _window.WindowCommandMode, value);
    }

    public string WindowSelectorKind
    {
        get => _window.WindowSelectorKind;
        set => SetScriptField(
            ref _window.WindowSelectorKind,
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
        get => _window.WindowSelectorValue;
        set => SetScriptField(ref _window.WindowSelectorValue, value ?? string.Empty);
    }

    public string WindowActiveField
    {
        get => _window.WindowActiveField;
        set => SetScriptField(
            ref _window.WindowActiveField,
            WindowActiveFieldSyntax.Normalize(value));
    }

    public string WindowOutputVariable
    {
        get => _window.WindowOutputVariable;
        set => SetScriptField(ref _window.WindowOutputVariable, value?.Trim() ?? string.Empty);
    }

    public int WindowTimeoutMs
    {
        get => _window.WindowTimeoutMs;
        set => SetScriptField(ref _window.WindowTimeoutMs, value);
    }

    public int WindowX
    {
        get => _window.WindowX;
        set => SetScriptField(ref _window.WindowX, value);
    }

    public int WindowY
    {
        get => _window.WindowY;
        set => SetScriptField(ref _window.WindowY, value);
    }

    public int WindowWidth
    {
        get => _window.WindowWidth;
        set => SetScriptField(ref _window.WindowWidth, value);
    }

    public int WindowHeight
    {
        get => _window.WindowHeight;
        set => SetScriptField(ref _window.WindowHeight, value);
    }

    public string WindowWorkspace
    {
        get => _window.WindowWorkspace;
        set => SetScriptField(ref _window.WindowWorkspace, value ?? string.Empty);
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
    public string DisplayName => ActionDisplayFormatter.Format(this);

    private static bool IsVariableCoordinateToken(string token)
    {
        return EditorActionScriptTokens.TryParseNumericToken(token, out var sourceType, out _)
            && sourceType is ScriptNumericSourceType.VariableReference;
    }

    private static bool IsValidImageSearchRegionToken(string token, bool mustBePositive)
    {
        if (!EditorActionScriptTokens.TryParseNumericToken(token, out var sourceType, out var value))
        {
            return false;
        }

        return !mustBePositive
            || sourceType is ScriptNumericSourceType.VariableReference
            || int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) > 0;
    }

    private void SetCoordinateToken(
        string? value,
        ref string? tokenOverride,
        ref int coordinate,
        string tokenPropertyName,
        string coordinatePropertyName)
    {
        var previousToken = tokenOverride ?? coordinate.ToString(CultureInfo.InvariantCulture);
        var token = value?.Trim() ?? string.Empty;
        var coordinateChanged = false;

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var literal))
        {
            coordinateChanged = coordinate != literal;
            coordinate = literal;
            tokenOverride = null;
            token = literal.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            tokenOverride = token;
        }

        if (!coordinateChanged && string.Equals(previousToken, token, StringComparison.Ordinal))
        {
            return;
        }

        if (coordinateChanged)
        {
            OnPropertyChanged(coordinatePropertyName);
        }

        OnPropertyChanged(tokenPropertyName);
        OnPropertyChanged(nameof(DisplayName));
    }

    private void SetImageSearchRegionToken(
        string? value,
        ref string? tokenOverride,
        ref int coordinate,
        string tokenPropertyName,
        string coordinatePropertyName)
    {
        var previousToken = tokenOverride ?? coordinate.ToString(CultureInfo.InvariantCulture);
        var token = value?.Trim() ?? string.Empty;
        var coordinateChanged = false;

        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var literal))
        {
            coordinateChanged = coordinate != literal;
            coordinate = literal;
            tokenOverride = null;
            token = literal.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            tokenOverride = token;
        }

        if (!coordinateChanged && string.Equals(previousToken, token, StringComparison.Ordinal))
        {
            return;
        }

        MarkStructuredScriptEdited();
        if (coordinateChanged)
        {
            OnPropertyChanged(coordinatePropertyName);
        }

        OnPropertyChanged(tokenPropertyName);
        OnPropertyChanged(nameof(DisplayName));
    }

    private void SetImageSearchRegionLiteral(
        int value,
        ref int coordinate,
        ref string? tokenOverride,
        string coordinatePropertyName,
        string tokenPropertyName)
    {
        if (coordinate == value && tokenOverride is null)
        {
            return;
        }

        var tokenChanged = tokenOverride is not null;
        coordinate = value;
        tokenOverride = null;
        MarkStructuredScriptEdited();
        OnPropertyChanged(coordinatePropertyName);
        if (tokenChanged)
        {
            OnPropertyChanged(tokenPropertyName);
        }

        OnPropertyChanged(nameof(DisplayName));
    }

    /// <summary>
    /// Validates this action.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid()
        => EditorActionValidationPolicy.IsValid(this);

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

        if (TryGetScreenReadingPayload(out var screenReadingPayload))
        {
            clone.ApplyScreenReadingPayload(screenReadingPayload);
            clone._screen.ImageSearchRegionLeftToken = _screen.ImageSearchRegionLeftToken;
            clone._screen.ImageSearchRegionTopToken = _screen.ImageSearchRegionTopToken;
            clone._screen.ImageSearchRegionWidthToken = _screen.ImageSearchRegionWidthToken;
            clone._screen.ImageSearchRegionHeightToken = _screen.ImageSearchRegionHeightToken;
            clone.PreferLegacyScriptText = PreferLegacyScriptText;
        }


        return clone;
    }

    private void CopyInputFields(EditorAction clone)
    {
        clone._type = Type;
        clone._input = _input.Copy();
    }

    private void CopyScriptFields(EditorAction clone) => clone._script = _script.Copy();

    private void SetMousePositionVariableName(ref string field, string? value, string propertyName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.Equals(field, normalized, StringComparison.Ordinal))
        {
            return;
        }

        field = normalized;
        MarkStructuredScriptEdited();
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(DisplayName));
    }

    private void CopyCommandFields(EditorAction clone)
    {
        clone._screen = _screen.Copy();
        clone._shell = _shell.Copy();
        clone._screenshot = _screenshot.Copy();
        clone._window = _window.Copy();
    }

    private bool UseLegacyScriptTextDisplay => PreferLegacyScriptText && !string.IsNullOrWhiteSpace(Text);

    private void ClearPreservedTextInputEvents()
    {
        _input.PreservedTextInputEvents = null;
        _input.PreservedTextInputText = null;
    }

    private void MarkStructuredScriptEdited()
    {
        if (EditorActionValidationPolicy.IsScriptPayloadAction(Type))
        {
            PreferLegacyScriptText = false;
        }
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
        // Expression values are already stored in canonical form and render verbatim;
        // the simple number and variable formatting path would corrupt them.
        if (ScriptNumericExpression.TryParse(value, out var expression) && expression is { Op: not null })
        {
            return value.Trim();
        }

        return EditorActionScriptTokens.FormatNumericToken(sourceType, value);
    }

    private static string BuildOperandToken(ScriptOperandType operandType, string value)
    {
        if (operandType is ScriptOperandType.Number or ScriptOperandType.VariableReference
            && ScriptNumericExpression.TryParse(value, out var expression) && expression is { Op: not null })
        {
            return value.Trim();
        }

        return EditorActionScriptTokens.FormatOperandToken(operandType, value);
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

    private string FormatWindowSelectorSummary(string verb)
    {
        return string.Equals(WindowSelectorKind, "active", StringComparison.Ordinal)
            ? $"{verb} active window"
            : $"{verb} window by {WindowSelectorKind} \"{WindowSelectorValue}\"";
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
