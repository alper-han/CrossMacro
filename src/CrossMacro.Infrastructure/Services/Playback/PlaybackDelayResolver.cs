
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Owns fixed/random delay resolution for event and runtime-script coordination.
/// </summary>
public sealed class PlaybackDelayResolver(Func<int, int, int>? randomInclusive = null)
{
    private readonly Func<int, int, int> _randomInclusive = randomInclusive ?? RandomNumberGeneratorUtility.GetInt32Inclusive;

    public int Resolve(int fixedDelayMs, bool hasRandomDelay, int randomDelayMinMs, int randomDelayMaxMs)
    {
        var randomDelay = 0;
        if (hasRandomDelay)
        {
            var min = Math.Min(randomDelayMinMs, randomDelayMaxMs);
            var max = Math.Max(randomDelayMinMs, randomDelayMaxMs);
            randomDelay = min == max ? min : _randomInclusive(min, max);
        }

        var totalDelay = (long)fixedDelayMs + randomDelay;
        if (totalDelay <= 0)
        {
            return 0;
        }
        if (totalDelay > int.MaxValue)
        {
            return int.MaxValue;
        }
        return (int)totalDelay;
    }
}
