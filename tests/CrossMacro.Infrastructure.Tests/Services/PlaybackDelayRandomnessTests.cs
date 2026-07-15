namespace CrossMacro.Infrastructure.Tests.Services;

public class PlaybackDelayResolverTests
{
    [Fact]
    public void PlaybackDelayResolver_UsesInclusiveRangeAndClampsResult()
    {
        var requestedRange = (min: 0, max: 0);
        var resolver = new PlaybackDelayResolver((min, max) =>
        {
            requestedRange = (min, max);
            return max;
        });

        resolver.Resolve(5, hasRandomDelay: true, 2, 4).Should().Be(9);
        requestedRange.Should().Be((2, 4));
    }

    [Fact]
    public void PlaybackDelayResolver_InvertsBoundsAndPreservesEqualBounds()
    {
        var invocationCount = 0;
        var requestedRange = (min: 0, max: 0);
        var resolver = new PlaybackDelayResolver((min, max) =>
        {
            invocationCount++;
            requestedRange = (min, max);
            return max;
        });

        resolver.Resolve(1, hasRandomDelay: true, 8, 3).Should().Be(9);
        requestedRange.Should().Be((3, 8));
        resolver.Resolve(1, hasRandomDelay: true, int.MaxValue, int.MaxValue).Should().Be(int.MaxValue);
        invocationCount.Should().Be(1);
    }

    [Fact]
    public void PlaybackDelayResolver_ClampsNegativeTotalDelayToZero()
    {
        var resolver = new PlaybackDelayResolver((min, max) => min);

        resolver.Resolve(-10, hasRandomDelay: false, 0, 0).Should().Be(0);
        resolver.Resolve(-10, hasRandomDelay: true, -4, -2).Should().Be(0);
    }

    [Fact]
    public void PlaybackDelayResolver_AllowsIntMaxValueEndpointAndSaturates()
    {
        var resolver = new PlaybackDelayResolver((min, max) => max);

        resolver.Resolve(1, hasRandomDelay: true, 0, int.MaxValue).Should().Be(int.MaxValue);
    }
}
