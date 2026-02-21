namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using CrossMacro.Infrastructure.Services;

public class SystemTimeProviderTests
{
    [Fact]
    public void TimeProperties_ShouldReturnRecentValues()
    {
        var provider = new SystemTimeProvider();
        var now = DateTime.Now;
        var utcNow = DateTime.UtcNow;

        Assert.True((provider.Now - now).Duration() < TimeSpan.FromSeconds(5));
        Assert.True((provider.UtcNow - utcNow).Duration() < TimeSpan.FromSeconds(5));
    }
}
