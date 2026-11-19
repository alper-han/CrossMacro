namespace CrossMacro.Platform.Abstractions.Input;

/// <summary>
/// Converts native wheel deltas, which can be fractions of a notch on precision touchpads and
/// high-resolution wheels, into the whole notches that recorded scroll events carry.
/// The remainder of one call carries over to the next so slow scrolling is not lost.
/// </summary>
public sealed class ScrollNotchAccumulator
{
    private long _remainder;

    /// <param name="nativeDelta">Delta in the platform's native wheel unit.</param>
    /// <param name="unitsPerNotch">Native units that make up one notch.</param>
    /// <returns>Whole notches now reached; positive scrolls up or right. Zero while less than one notch has built up.</returns>
    public int Add(long nativeDelta, long unitsPerNotch)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(unitsPerNotch, 0);

        if (nativeDelta is 0)
        {
            return 0;
        }

        if (_remainder is not 0 && Math.Sign(_remainder) != Math.Sign(nativeDelta))
        {
            _remainder = 0;
        }

        long total = _remainder + nativeDelta;
        _remainder = total % unitsPerNotch;
        return (int)Math.Clamp(total / unitsPerNotch, int.MinValue, int.MaxValue);
    }
}
