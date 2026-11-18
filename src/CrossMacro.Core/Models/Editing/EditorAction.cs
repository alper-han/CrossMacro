
namespace CrossMacro.Core.Models.Editing;

/// <summary>
/// Represents a single action in the macro editor.
/// Provides a user-friendly abstraction over MacroEvent for editing.
/// Implements INotifyPropertyChanged for proper UI binding.
/// </summary>
public partial class EditorAction : INotifyPropertyChanged
{
    private Guid _id;
    private EditorActionType _type;
    private readonly InputState _input;
    private readonly ScriptState _script;
    private readonly ScreenState _screen;
    private readonly ShellState _shell;
    private readonly ScreenshotState _screenshot;
    private readonly WindowState _window;
    private string _comment;

    public EditorAction()
    {
        _id = Guid.NewGuid();
        _input = new();
        _script = new();
        _screen = new();
        _shell = new();
        _screenshot = new();
        _window = new();
        _comment = string.Empty;
    }

    private EditorAction(EditorAction source)
    {
        _id = Guid.NewGuid();
        _type = source._type;
        _input = source._input.Copy();
        _script = source._script.Copy();
        _screen = source._screen.Copy();
        _shell = source._shell.Copy();
        _screenshot = source._screenshot.Copy();
        _window = source._window.Copy();
        _comment = source._comment;
    }

    /// <summary>
    /// Optional editor-only note associated with this action.
    /// </summary>
    public string Comment
    {
        get => _comment;
        set => SetField(ref _comment, value ?? string.Empty);
    }

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
        set => SetField(ref _id, value);
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

                if (!EditorActionScriptClassifier.IsScriptPayloadAction(value))
                {
                    PreferLegacyScriptText = false;
                }
                else if (!string.IsNullOrWhiteSpace(_input.Text))
                {
                    PreferLegacyScriptText = true;
                }
                OnPropertyChanged();
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
        (_input.CoordinateXToken is { } xToken && IsVariableCoordinateToken(xToken))
        || (_input.CoordinateYToken is { } yToken && IsVariableCoordinateToken(yToken));

    public bool TryGetLiteralCoordinates(out int x, out int y)
    {
        x = _input.X;
        y = _input.Y;
        var hasX = _input.CoordinateXToken is null
            || int.TryParse(_input.CoordinateXToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out x);
        var hasY = _input.CoordinateYToken is null
            || int.TryParse(_input.CoordinateYToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
        return hasX && hasY;
    }

    /// <summary>
    /// Whether coordinates are absolute (true) or relative (false).
    /// Used by mouse actions with coordinates (MouseMove/MouseClick/MouseDown/MouseUp).
    /// </summary>
    public bool IsAbsolute
    {
        get => _input.IsAbsolute;
        set => SetField(ref _input.IsAbsolute, value);
    }

    /// <summary>
    /// Unit space for relative mouse coordinates. Newly authored relative editor
    /// actions use direct raw input deltas; logical desktop pixels are opt-in.
    /// </summary>
    public MouseCoordinateSpace CoordinateSpace
    {
        get => _input.CoordinateSpace;
        set => SetField(ref _input.CoordinateSpace, value);
    }

    /// <summary>
    /// Mouse button (for click/down/up actions).
    /// </summary>
    public MacroMouseButton Button
    {
        get => _input.Button;
        set => SetField(ref _input.Button, value);
    }

    /// <summary>
    /// Keyboard key code (for key actions).
    /// Uses Linux input key codes.
    /// </summary>
    public int KeyCode
    {
        get => _input.KeyCode;
        set => SetField(ref _input.KeyCode, value);
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
        set => SetField(ref _input.UseRandomDelay, value);
    }

    /// <summary>
    /// Whether a mouse click should use the current cursor position at playback time.
    /// Only applicable for MouseClick actions.
    /// </summary>
    public bool UseCurrentPosition
    {
        get => _input.UseCurrentPosition;
        set => SetField(ref _input.UseCurrentPosition, value);
    }

    /// <summary>
    /// Minimum randomized delay in milliseconds.
    /// </summary>
    public int RandomDelayMinMs
    {
        get => _input.RandomDelayMinMs;
        set => SetField(ref _input.RandomDelayMinMs, value);
    }

    /// <summary>
    /// Maximum randomized delay in milliseconds.
    /// </summary>
    public int RandomDelayMaxMs
    {
        get => _input.RandomDelayMaxMs;
        set => SetField(ref _input.RandomDelayMaxMs, value);
    }

    /// <summary>
    /// Scroll amount (positive = up/right, negative = down/left).
    /// </summary>
    public int ScrollAmount
    {
        get => _input.ScrollAmount;
        set => SetField(ref _input.ScrollAmount, value);
    }

    /// <summary>
    /// Human-readable key name for display purposes.
    /// </summary>
    public string? KeyName
    {
        get => _input.KeyName;
        set => SetField(ref _input.KeyName, value);
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

                if (EditorActionScriptClassifier.IsScriptPayloadAction(Type))
                {
                    PreferLegacyScriptText = !string.IsNullOrWhiteSpace(_input.Text);
                }
                OnPropertyChanged();
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

    public int ScreenX
    {
        get => _screen.ScreenX;
        set => SetScriptField(ref _screen.ScreenX, value);
    }

    public int ScreenY
    {
        get => _screen.ScreenY;
        set => SetScriptField(ref _screen.ScreenY, value);
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
        set => SetScriptField(ref _screen.ScreenColorHex, NormalizeColorHex(value));
    }

    public EditorActionScreenTargetColorSource ScreenTargetColorSource
    {
        get => _screen.ScreenTargetColorSource;
        set => SetScriptField(ref _screen.ScreenTargetColorSource, value);
    }

    public string ScreenTargetColorVariableName
    {
        get => _screen.ScreenTargetColorVariableName;
        set => SetScriptField(ref _screen.ScreenTargetColorVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenColorVariableName
    {
        get => _screen.ScreenColorVariableName;
        set => SetScriptField(ref _screen.ScreenColorVariableName, value?.Trim() ?? string.Empty);
    }

    public int ScreenTimeoutMs
    {
        get => _screen.ScreenTimeoutMs;
        set => SetScriptField(ref _screen.ScreenTimeoutMs, value);
    }

    public int ScreenTolerance
    {
        get => _screen.ScreenTolerance;
        set => SetScriptField(ref _screen.ScreenTolerance, value);
    }

    public string ScreenFoundVariableName
    {
        get => _screen.ScreenFoundVariableName;
        set => SetScriptField(ref _screen.ScreenFoundVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenFoundXVariableName
    {
        get => _screen.ScreenFoundXVariableName;
        set => SetScriptField(ref _screen.ScreenFoundXVariableName, value?.Trim() ?? string.Empty);
    }

    public string ScreenFoundYVariableName
    {
        get => _screen.ScreenFoundYVariableName;
        set => SetScriptField(ref _screen.ScreenFoundYVariableName, value?.Trim() ?? string.Empty);
    }

    public string ImageAssetName
    {
        get => _screen.ImageAssetName;
        set => SetScriptField(ref _screen.ImageAssetName, value?.Trim() ?? string.Empty);
    }

    public double ImageSearchSimilarity
    {
        get => _screen.ImageSearchSimilarity;
        set => SetScriptField(ref _screen.ImageSearchSimilarity, value);
    }

    public EditorImageMatchMode ImageSearchMatchMode
    {
        get => _screen.ImageSearchMatchMode;
        set => SetScriptField(ref _screen.ImageSearchMatchMode, value);
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

    public bool TryGetScreenshotStep(out ScreenshotStep step)
    {
        step = new ScreenshotStep(
            ScreenshotOutputPath,
            ScreenshotCopyToClipboard,
            ScreenshotUseRegion,
            ScreenshotRegionX,
            ScreenshotRegionY,
            ScreenshotRegionWidth,
            ScreenshotRegionHeight);
        return Type is EditorActionType.Screenshot;
    }

    public bool TryGetShellPayload(out EditorActionShellPayload payload) =>
        EditorActionShellPayload.TryCreate(this, out payload);

    public bool TryGetWindowPayload(out EditorActionWindowPayload payload) =>
        EditorActionWindowPayload.TryCreate(this, out payload);

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
        if (TryApplyCoordinateToken(value, ref tokenOverride, ref coordinate, out var coordinateChanged))
        {
            NotifyCoordinateTokenChanged(coordinateChanged, tokenPropertyName, coordinatePropertyName);
        }
    }

    private void SetImageSearchRegionToken(
        string? value,
        ref string? tokenOverride,
        ref int coordinate,
        string tokenPropertyName,
        string coordinatePropertyName)
    {
        if (!TryApplyCoordinateToken(value, ref tokenOverride, ref coordinate, out var coordinateChanged))
        {
            return;
        }

        MarkStructuredScriptEdited();
        NotifyCoordinateTokenChanged(coordinateChanged, tokenPropertyName, coordinatePropertyName);
    }

    private static bool TryApplyCoordinateToken(
        string? value,
        ref string? tokenOverride,
        ref int coordinate,
        out bool coordinateChanged)
    {
        var previousToken = tokenOverride ?? coordinate.ToString(CultureInfo.InvariantCulture);
        var token = value?.Trim() ?? string.Empty;
        coordinateChanged = false;

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

        return coordinateChanged || !string.Equals(previousToken, token, StringComparison.Ordinal);
    }

    private void NotifyCoordinateTokenChanged(bool coordinateChanged, string tokenPropertyName, string coordinatePropertyName)
    {
        if (coordinateChanged)
        {
            OnPropertyChanged(coordinatePropertyName);
        }

        OnPropertyChanged(tokenPropertyName);
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
    }

    public EditorAction Clone() => new(this);

    private void ClearPreservedTextInputEvents()
    {
        _input.PreservedTextInputEvents = null;
        _input.PreservedTextInputText = null;
    }

    private void MarkStructuredScriptEdited()
    {
        if (EditorActionScriptClassifier.IsScriptPayloadAction(Type))
        {
            PreferLegacyScriptText = false;
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
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
    }

    private static string NormalizeColorHex(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
