namespace CrossMacro.Core.Services;

/// <summary>
/// Applies runtime log-level changes to the current process.
/// </summary>
public interface IRuntimeLogLevelService
{
    public void SetLogLevel(string logLevel);
}
