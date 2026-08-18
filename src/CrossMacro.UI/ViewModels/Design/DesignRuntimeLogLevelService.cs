
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignRuntimeLogLevelService : IRuntimeLogLevelService
{
    public string CurrentLogLevel { get; private set; } = "Information";

    public void SetLogLevel(string logLevel)
    {
        CurrentLogLevel = logLevel;
    }
}
