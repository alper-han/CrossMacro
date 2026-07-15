
namespace CrossMacro.Daemon.Services;

public sealed class RateLimiterService : IRateLimiterService
{
    private readonly RateLimiter _inner;

    public RateLimiterService(RateLimiter inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public bool IsRateLimited(uint uid) => _inner.IsRateLimited(uid);
    public void RecordSuccess(uint uid) => _inner.RecordSuccess(uid);
}
