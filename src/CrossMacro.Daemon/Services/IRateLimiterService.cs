namespace CrossMacro.Daemon.Services;

public interface IRateLimiterService
{
    bool IsRateLimited(uint uid);
    void RecordSuccess(uint uid);
}
