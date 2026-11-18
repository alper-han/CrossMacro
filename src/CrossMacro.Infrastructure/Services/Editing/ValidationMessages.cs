namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>
/// Validation error messages for editor action validation. Limits are formatted from
/// <see cref="EditorActionValidationLimits"/> so the text cannot drift from the rules.
/// </summary>
internal static class ValidationMessages
{
    private const int MillisecondsPerHour = 3_600_000;

    // General
    public const string ActionCannotBeNull = "Action cannot be null";

    // Delay
    public const string DelayMustBeNonNegative = "Delay must be non-negative";
    public const string DelayMustBePositive = "Delay must be greater than 0ms";
    public const string RandomDelayBoundsInvalid = "Random delay maximum must be greater than or equal to minimum";

    public static readonly string DelayTooLong = Format($"Delay cannot exceed {EditorActionValidationLimits.MaxDelayMs / MillisecondsPerHour} hours");

    // Key Actions
    public const string KeyCodeMustBePositive = "Key code must be positive";

    public static readonly string KeyCodeInvalid = Format($"Invalid key code (max: {EditorActionValidationLimits.MaxKeyCode})");

    // Scroll
    public const string ScrollAmountCannotBeZero = "Scroll amount cannot be zero";

    public static readonly string ScrollAmountTooLarge = Format($"Scroll amount cannot exceed {EditorActionValidationLimits.MaxScrollAmount}");

    // MouseMove
    public const string CoordinateTokenInvalid = "Coordinates must be integers or variable references";
    public const string AbsoluteCoordsOutOfRange = "Absolute coordinates are outside the supported range";
    public const string RelativeMoveMustHaveValue = "Relative move must have non-zero X or Y";

    public static readonly string RelativeMoveTooLarge = Format($"Relative movement too large (max: ±{EditorActionValidationLimits.MaxRelativeCoordinateDelta})");

    // MacroMouseButton
    public const string InvalidMouseButton = "Invalid mouse button";
    public const string UseScrollActionForScrollButtons = "Use Scroll action for scroll buttons";
    public const string CurrentPositionClickMustNotUseCoordinates = "Current-position click must not carry coordinates";

    // Text / Script
    public const string TextInputRequired = "Text content is required";
    public const string ActionPayloadRequired = "Action payload is required";

    private static string Format(FormattableString message) => message.ToString(CultureInfo.InvariantCulture);
}
