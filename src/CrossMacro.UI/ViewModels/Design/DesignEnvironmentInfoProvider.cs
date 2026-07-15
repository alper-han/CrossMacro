
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignEnvironmentInfoProvider : IEnvironmentInfoProvider
{
    public DisplayEnvironment CurrentEnvironment => DisplayEnvironment.LinuxGnome;

    public bool WindowManagerHandlesCloseButton => false;
}
