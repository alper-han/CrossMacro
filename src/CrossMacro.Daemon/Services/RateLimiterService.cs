
namespace CrossMacro.Daemon.Services;

internal sealed class RateLimiterService(RateLimiter inner) : IRateLimiterService
{
    private readonly RateLimiter _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public bool IsRateLimited(uint uid) => _inner.IsRateLimited(uid);
    public void RecordSuccess(uint uid) => _inner.RecordSuccess(uid);
}
