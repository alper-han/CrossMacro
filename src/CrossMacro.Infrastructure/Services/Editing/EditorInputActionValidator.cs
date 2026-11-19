namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Validates delay, key, scroll, mouse and text-input actions.</summary>
internal static class EditorInputActionValidator
{
    internal static (bool IsValid, string? Error) ValidateDelay(EditorAction action)
    {
        if (action.UseRandomDelay)
        {
            return ValidateRandomDelay(action);
        }

        if (action.DelayMicroseconds < 0)
        {
            return (false, ValidationMessages.DelayMustBeNonNegative);
        }

        if (action.DelayMicroseconds is 0)
        {
            return (false, ValidationMessages.DelayMustBePositive);
        }

        if (action.DelayMicroseconds > (long)EditorActionValidationLimits.MaxDelayMs * MacroTiming.MicrosecondsPerMillisecond)
        {
            return (false, ValidationMessages.DelayTooLong);
        }

        return (true, null);
    }

    internal static (bool IsValid, string? Error) ValidateKeyAction(EditorAction action)
    {
        if (action.KeyCode <= 0)
        {
            return (false, ValidationMessages.KeyCodeMustBePositive);
        }

        if (action.KeyCode > EditorActionValidationLimits.MaxKeyCode)
        {
            return (false, ValidationMessages.KeyCodeInvalid);
        }

        return (true, null);
    }

    internal static (bool IsValid, string? Error) ValidateScroll(EditorAction action)
    {
        if (action.ScrollAmount is 0)
        {
            return (false, ValidationMessages.ScrollAmountCannotBeZero);
        }

        if (Math.Abs((long)action.ScrollAmount) > EditorActionValidationLimits.MaxScrollAmount)
        {
            return (false, ValidationMessages.ScrollAmountTooLarge);
        }

        return (true, null);
    }

    internal static (bool IsValid, string? Error) ValidateMouseMove(EditorAction action) =>
        ValidateCoordinateBounds(action, requireRelativeNonZero: true);

    internal static (bool IsValid, string? Error) ValidateMouseButton(EditorAction action)
    {
        if (!Enum.IsDefined(action.Button))
        {
            return (false, ValidationMessages.InvalidMouseButton);
        }

        if (action.Button is MacroMouseButton.ScrollUp or MacroMouseButton.ScrollDown
            or MacroMouseButton.ScrollLeft or MacroMouseButton.ScrollRight)
        {
            return (false, ValidationMessages.UseScrollActionForScrollButtons);
        }

        if (action.UseCurrentPosition && action.IsAbsolute)
        {
            return (false, ValidationMessages.CurrentPositionClickMustNotUseCoordinates);
        }

        return ValidateCoordinateBounds(action, requireRelativeNonZero: false);
    }

    internal static (bool IsValid, string? Error) ValidateTextInput(EditorAction action) =>
        string.IsNullOrEmpty(action.Text) ? (false, ValidationMessages.TextInputRequired) : (true, null);

    private static (bool IsValid, string? Error) ValidateRandomDelay(EditorAction action)
    {
        if (action.RandomDelayMinMs < 0 || action.RandomDelayMaxMs < 0)
        {
            return (false, ValidationMessages.DelayMustBeNonNegative);
        }

        if (action.RandomDelayMaxMs < action.RandomDelayMinMs)
        {
            return (false, ValidationMessages.RandomDelayBoundsInvalid);
        }

        if (action.RandomDelayMinMs is 0 && action.RandomDelayMaxMs is 0)
        {
            return (false, ValidationMessages.DelayMustBePositive);
        }

        if (action.RandomDelayMaxMs > EditorActionValidationLimits.MaxDelayMs)
        {
            return (false, ValidationMessages.DelayTooLong);
        }

        return (true, null);
    }

    private static (bool IsValid, string? Error) ValidateCoordinateBounds(EditorAction action, bool requireRelativeNonZero)
    {
        if (!EditorActionScriptTokens.TryParseNumericToken(action.CoordinateXToken, out _, out _)
            || !EditorActionScriptTokens.TryParseNumericToken(action.CoordinateYToken, out _, out _))
        {
            return (false, ValidationMessages.CoordinateTokenInvalid);
        }

        var hasLiteralX = int.TryParse(action.CoordinateXToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x);
        var hasLiteralY = int.TryParse(action.CoordinateYToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y);

        if (action.IsAbsolute)
        {
            if ((hasLiteralX && Math.Abs((long)x) > EditorActionValidationLimits.MaxAbsoluteCoordinate)
                || (hasLiteralY && Math.Abs((long)y) > EditorActionValidationLimits.MaxAbsoluteCoordinate))
            {
                return (false, ValidationMessages.AbsoluteCoordsOutOfRange);
            }

            return (true, null);
        }

        if (requireRelativeNonZero && hasLiteralX && hasLiteralY && x is 0 && y is 0)
        {
            return (false, ValidationMessages.RelativeMoveMustHaveValue);
        }

        if ((hasLiteralX && Math.Abs((long)x) > EditorActionValidationLimits.MaxRelativeCoordinateDelta)
            || (hasLiteralY && Math.Abs((long)y) > EditorActionValidationLimits.MaxRelativeCoordinateDelta))
        {
            return (false, ValidationMessages.RelativeMoveTooLarge);
        }

        return (true, null);
    }
}
