namespace CrossMacro.Daemon.Services;

internal interface IRateLimiterService
{
    public bool IsRateLimited(uint uid);
    public void RecordSuccess(uint uid);
}
