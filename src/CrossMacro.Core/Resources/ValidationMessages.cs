namespace CrossMacro.Core.Resources;

/// <summary>
/// Centralized validation error messages for EditorAction validation.
/// </summary>
public static class ValidationMessages
{
    // General
    public const string ActionCannotBeNull = "Action cannot be null";
    
    // Delay
    public const string DelayMustBeNonNegative = "Delay must be non-negative";
    public const string DelayMustBePositive = "Delay must be greater than 0ms";
    public const string DelayTooLong = "Delay cannot exceed 1 hour";
    public const string RandomDelayBoundsInvalid = "Random delay maximum must be greater than or equal to minimum";
    
    // Key Actions
    public const string KeyCodeMustBePositive = "Key code must be positive";
    public const string KeyCodeInvalid = "Invalid key code (max: 767)";
    
    // Scroll
    public const string ScrollAmountCannotBeZero = "Scroll amount cannot be zero";
    public const string ScrollAmountTooLarge = "Scroll amount cannot exceed 100";
    
    // MouseMove
    public const string AbsoluteCoordsMustBeNonNegative = "Absolute coordinates must be non-negative";
    public const string CoordsExceedMaximum = "Coordinates exceed maximum supported value";
    public const string RelativeMoveMustHaveValue = "Relative move must have non-zero X or Y";
    public const string RelativeMoveTooLarge = "Relative movement too large (max: ±10000)";
    
    // MouseButton
    public const string InvalidMouseButton = "Invalid mouse button";
    public const string UseScrollActionForScrollButtons = "Use Scroll action for scroll buttons";
}
