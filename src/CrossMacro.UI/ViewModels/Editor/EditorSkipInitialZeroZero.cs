namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Owns the "skip the initial 0,0 move" option. Current-position mouse buttons force it on; this remembers the
/// user's own choice so it can be restored once the last such action is gone.
/// </summary>
internal sealed class EditorSkipInitialZeroZero(EditorViewModel owner)
{
    private bool _forcedByCurrentPosition;
    private bool _valueBeforeForce;

    internal bool Value { get; private set; }

    /// <summary>Applies a user edit; returns whether the value changed.</summary>
    internal bool TrySet(bool value)
    {
        var normalized = owner.RequiresSkipInitialZeroZero || value;
        if (Value == normalized)
        {
            return false;
        }

        Value = normalized;
        if (!owner.RequiresSkipInitialZeroZero && !_forcedByCurrentPosition)
        {
            _valueBeforeForce = normalized;
        }

        return true;
    }

    /// <summary>Applies the value resolved while building a macro; returns whether it changed.</summary>
    internal bool Apply(bool value)
    {
        if (Value == value)
        {
            return false;
        }

        Value = value;
        return true;
    }

    /// <summary>Takes the value of a freshly loaded sequence.</summary>
    internal void Load(bool sequenceValue, bool hasCurrentPositionMouseButtons)
    {
        Value = sequenceValue || hasCurrentPositionMouseButtons;
        _forcedByCurrentPosition = hasCurrentPositionMouseButtons;
        _valueBeforeForce = sequenceValue;
    }

    /// <summary>Forces the option on while a current-position button exists, and restores the user's choice afterwards.</summary>
    internal void Refresh()
    {
        if (owner.RequiresSkipInitialZeroZero)
        {
            if (!_forcedByCurrentPosition)
            {
                _valueBeforeForce = Value;
                _forcedByCurrentPosition = true;
            }

            Value = true;
        }
        else if (_forcedByCurrentPosition)
        {
            Value = _valueBeforeForce;
            _forcedByCurrentPosition = false;
        }
    }
}
