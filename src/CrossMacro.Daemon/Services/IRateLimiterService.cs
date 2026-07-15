namespace CrossMacro.Daemon.Services;

public interface IRateLimiterService
{
    public bool IsRateLimited(uint uid);
    public void RecordSuccess(uint uid);
}
