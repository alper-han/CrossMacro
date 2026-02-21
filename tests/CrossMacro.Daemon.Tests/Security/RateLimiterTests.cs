using System;
using CrossMacro.Daemon.Security;
using Xunit;

namespace CrossMacro.Daemon.Tests.Security;

public class RateLimiterTests
{
    private sealed class FakeClock
    {
        public DateTime UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DateTime Now() => UtcNow;

        public void Advance(TimeSpan duration)
        {
            UtcNow = UtcNow.Add(duration);
        }
    }

    [Fact]
    public void IsRateLimited_FirstAttempt_ShouldNotBeLimited()
    {
        var clock = new FakeClock();
        var limiter = new RateLimiter(maxConnectionsPerWindow: 2, windowSeconds: 10, banSeconds: 1, utcNow: clock.Now);

        var limited = limiter.IsRateLimited(uid: 1000);

        Assert.False(limited);
    }

    [Fact]
    public void IsRateLimited_WhenLimitExceeded_ShouldBanUid()
    {
        var clock = new FakeClock();
        var limiter = new RateLimiter(maxConnectionsPerWindow: 2, windowSeconds: 10, banSeconds: 5, utcNow: clock.Now);
        const uint uid = 1000;

        _ = limiter.IsRateLimited(uid);
        _ = limiter.IsRateLimited(uid);

        var thirdAttemptLimited = limiter.IsRateLimited(uid);
        var status = limiter.GetStatus(uid);

        Assert.True(thirdAttemptLimited);
        Assert.True(status.isBanned);
    }

    [Fact]
    public void IsRateLimited_AfterBanExpires_ShouldAllowAgain()
    {
        var clock = new FakeClock();
        var limiter = new RateLimiter(maxConnectionsPerWindow: 1, windowSeconds: 10, banSeconds: 1, utcNow: clock.Now);
        const uint uid = 1000;

        _ = limiter.IsRateLimited(uid);
        _ = limiter.IsRateLimited(uid); // banned here

        clock.Advance(TimeSpan.FromSeconds(2));

        var limitedAfterBan = limiter.IsRateLimited(uid);

        Assert.False(limitedAfterBan);
    }

    [Fact]
    public void IsRateLimited_AfterWindowExpires_ShouldResetCounter()
    {
        var clock = new FakeClock();
        var limiter = new RateLimiter(maxConnectionsPerWindow: 1, windowSeconds: 1, banSeconds: 10, utcNow: clock.Now);
        const uint uid = 1000;

        _ = limiter.IsRateLimited(uid);
        clock.Advance(TimeSpan.FromSeconds(2));

        var limitedAfterWindowReset = limiter.IsRateLimited(uid);

        Assert.False(limitedAfterWindowReset);
    }

    [Fact]
    public void RecordSuccess_ShouldReduceAttemptCount()
    {
        var clock = new FakeClock();
        var limiter = new RateLimiter(maxConnectionsPerWindow: 5, windowSeconds: 10, banSeconds: 1, utcNow: clock.Now);
        const uint uid = 1000;

        _ = limiter.IsRateLimited(uid);
        _ = limiter.IsRateLimited(uid);
        _ = limiter.IsRateLimited(uid);

        limiter.RecordSuccess(uid);
        var status = limiter.GetStatus(uid);

        Assert.Equal(2, status.attemptCount);
    }

    [Fact]
    public void IsRateLimited_ShouldTrackEachUidSeparately()
    {
        var clock = new FakeClock();
        var limiter = new RateLimiter(maxConnectionsPerWindow: 1, windowSeconds: 10, banSeconds: 5, utcNow: clock.Now);

        _ = limiter.IsRateLimited(uid: 1000);
        var user1Limited = limiter.IsRateLimited(uid: 1000); // exceed for user1

        var user2Limited = limiter.IsRateLimited(uid: 2000); // first attempt for user2

        Assert.True(user1Limited);
        Assert.False(user2Limited);
    }
}
