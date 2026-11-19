namespace CrossMacro.Platform.MacOS.Services.Input;

/// <summary>
/// Converts between CoreGraphics scroll deltas and the whole wheel notches used by
/// <see cref="InputEventCode.REL_WHEEL"/> and <see cref="IInputSimulator.Scroll"/>.
/// CoreGraphics reports horizontal scrolling with the opposite sign (positive is left).
/// </summary>
internal sealed class MacOSScrollTranslator
{
    internal const long FixedPointUnitsPerLine = 1L << 16;

    private readonly ScrollNotchAccumulator _vertical = new();
    private readonly ScrollNotchAccumulator _horizontal = new();

    /// <summary>
    /// Fractional lines of a trackpad or high-resolution wheel come from the 16.16 fixed-point field;
    /// the integer line field is the fallback when the event carries none.
    /// </summary>
    internal static long ToFixedPointLines(long lineDelta, long fixedPointLineDelta) =>
        fixedPointLineDelta is not 0 ? fixedPointLineDelta : lineDelta * FixedPointUnitsPerLine;

    internal int VerticalNotches(long lineDelta, long fixedPointLineDelta) =>
        _vertical.Add(ToFixedPointLines(lineDelta, fixedPointLineDelta), FixedPointUnitsPerLine);

    internal int HorizontalNotches(long lineDelta, long fixedPointLineDelta) =>
        _horizontal.Add(-ToFixedPointLines(lineDelta, fixedPointLineDelta), FixedPointUnitsPerLine);

    /// <summary>Wheel arguments for <c>CGEventCreateScrollWheelEvent2</c> that scroll by <paramref name="notches"/> lines.</summary>
    internal static (uint WheelCount, int Wheel1, int Wheel2) ToWheelValues(int notches, bool isHorizontal) =>
        isHorizontal
            ? (2u, 0, (int)Math.Clamp(-(long)notches, int.MinValue, int.MaxValue))
            : (1u, notches, 0);
}
