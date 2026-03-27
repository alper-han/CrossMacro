namespace CrossMacro.Core.Models;

/// <summary>
/// Shared numeric limits used by editor action validation and related UI hints.
/// </summary>
public static class EditorActionValidationLimits
{
    public const int MaxTextInputLength = 500;
    public const int MaxDelayMs = 3_600_000; // 1 hour
    public const int MaxKeyCode = 767;
    public const int MaxScrollAmount = 100;
    public const int MaxAbsoluteCoordinate = 32_767;
    public const int MaxRelativeCoordinateDelta = 10_000;
}
