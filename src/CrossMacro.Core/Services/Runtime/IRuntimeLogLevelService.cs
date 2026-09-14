namespace CrossMacro.Core.Services.Runtime;

/// <summary>
/// Applies runtime log-level changes to the current process.
/// </summary>
public interface IRuntimeLogLevelService
{
    public void SetLogLevel(string logLevel);
}
