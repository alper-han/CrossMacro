using CrossMacro.Core.Models.Macros;

namespace CrossMacro.Editing.Models;

public partial class EditorAction
{
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

    public IReadOnlyList<MacroEvent>? GetPreservedTextInputEvents()
    {
        return Type is EditorActionType.TextInput
            && _input.PreservedTextInputEvents is { Count: > 0 }
            && string.Equals(_input.PreservedTextInputText, Text, StringComparison.Ordinal)
            ? _input.PreservedTextInputEvents
            : null;
    }

    public void PreserveTextInputEvents(IEnumerable<MacroEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        _input.PreservedTextInputEvents = events.ToList();
        _input.PreservedTextInputText = Text;
    }

    private void ClearPreservedTextInputEvents()
    {
        _input.PreservedTextInputEvents = null;
        _input.PreservedTextInputText = null;
    }
}
