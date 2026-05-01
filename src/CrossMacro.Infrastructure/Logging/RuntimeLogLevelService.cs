using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Logging;

/// <summary>
/// Adapts runtime log-level changes to the shared logger bootstrap.
/// </summary>
public sealed class RuntimeLogLevelService : IRuntimeLogLevelService
{
    public void SetLogLevel(string logLevel)
    {
        LoggerSetup.SetLogLevel(logLevel);
    }
}
