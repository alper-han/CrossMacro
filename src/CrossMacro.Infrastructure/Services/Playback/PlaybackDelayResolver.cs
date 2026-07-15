
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Owns fixed/random delay resolution for event and runtime-script coordination.
/// </summary>
internal sealed class PlaybackDelayResolver
{
    private readonly Random _random;

    public PlaybackDelayResolver(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public int Resolve(int fixedDelayMs, bool hasRandomDelay, int randomDelayMinMs, int randomDelayMaxMs)
    {
        var randomDelay = 0;
        if (hasRandomDelay)
        {
            var min = Math.Min(randomDelayMinMs, randomDelayMaxMs);
            var max = Math.Max(randomDelayMinMs, randomDelayMaxMs);
            randomDelay = min == max
                ? min
                : max == int.MaxValue
                    ? (int)_random.NextInt64(min, (long)max + 1)
                    : _random.Next(min, max + 1);
        }

        var totalDelay = (long)fixedDelayMs + randomDelay;
        return totalDelay <= 0 ? 0 : totalDelay > int.MaxValue ? int.MaxValue : (int)totalDelay;
    }
}
