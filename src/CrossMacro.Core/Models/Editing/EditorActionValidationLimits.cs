namespace CrossMacro.Core.Models.Editing;

/// <summary>
/// Shared numeric limits used by editor action validation and related UI hints.
/// </summary>
public static class EditorActionValidationLimits
{
    public const int MaxDelayMs = 86_400_000; // 24 hours (1 day)
    public const int MaxKeyCode = 767;
    public const int MaxScrollAmount = 100_000;
    public const int MaxAbsoluteCoordinate = 32_767;
    public const int MaxRelativeCoordinateDelta = 10_000;
}
