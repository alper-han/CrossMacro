
namespace CrossMacro.Daemon.Tests.Services;

public sealed class SecurityDependenciesTests
{
    [Fact]
    public void RateLimiterService_Ctor_WhenInnerNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new RateLimiterService(null!));
    }

    [Fact]
    public void SecurityAuditLogger_Ctor_WhenInnerNull_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new SecurityAuditLogger(null!));
    }

    [Fact]
    public async Task SecurityAuditLogger_LogSimulation_DelegatesToInnerAuditLogger()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"crossmacro-audit-adapter-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);

        try
        {
            var inner = new AuditLogger(directory, logSimulations: true);
            var adapter = new SecurityAuditLogger(inner);

            adapter.LogSimulation(1000, 123, type: 1, code: 2, value: 3);
            await adapter.DisposeAsync();

            var text = await File.ReadAllTextAsync(Path.Combine(directory, "audit.log"));
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
