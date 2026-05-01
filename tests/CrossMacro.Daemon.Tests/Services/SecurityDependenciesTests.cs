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
    public void SecurityAuditLogger_LogSimulation_DelegatesToInnerAuditLogger()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"crossmacro-audit-adapter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            var inner = new AuditLogger(directory, logSimulations: true);
            var adapter = new SecurityAuditLogger(inner);

            adapter.LogSimulation(1000, 123, type: 1, code: 2, value: 3);
            inner.Dispose();

            var text = File.ReadAllText(Path.Combine(directory, "audit.log"));
            Assert.Contains("UID=1000|PID=123|SIMULATE|type=1 code=2 value=3", text, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
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
