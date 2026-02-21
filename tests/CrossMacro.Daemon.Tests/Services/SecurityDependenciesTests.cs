using CrossMacro.Daemon.Security;
using CrossMacro.Daemon.Services;
using Xunit;

namespace CrossMacro.Daemon.Tests.Services;

public class SecurityDependenciesTests
{
    [Fact]
    public void RateLimiterService_Ctor_WhenInnerNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RateLimiterService(null!));
    }

    [Fact]
    public void SecurityAuditLogger_Ctor_WhenInnerNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SecurityAuditLogger(null!));
    }

    [Fact]
    public void RateLimiterService_DelegatesToInnerRateLimiter()
    {
        var inner = new RateLimiter(maxConnectionsPerWindow: 1, windowSeconds: 60, banSeconds: 60);
        var service = new RateLimiterService(inner);
        const uint uid = 1234;

        var first = service.IsRateLimited(uid);
        var second = service.IsRateLimited(uid);

        Assert.False(first);
        Assert.True(second);
    }
}
