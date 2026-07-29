
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignEnvironmentInfoProvider : IEnvironmentInfoProvider
{
    public DisplayEnvironment CurrentEnvironment => DisplayEnvironment.LinuxGnome;

    public bool WindowManagerHandlesCloseButton => false;
}
