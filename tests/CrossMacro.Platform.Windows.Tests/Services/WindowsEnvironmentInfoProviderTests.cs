using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Windows.Tests.Services;

public class WindowsEnvironmentInfoProviderTests
{
    [WindowsFact]
    public void CurrentEnvironment_ReturnsWindows()
    {
        var provider = new WindowsEnvironmentInfoProvider();

        Assert.Equal(DisplayEnvironment.Windows, provider.CurrentEnvironment);
    }

    [WindowsFact]
    public void WindowManagerHandlesCloseButton_ReturnsFalse()
    {
        var provider = new WindowsEnvironmentInfoProvider();

        Assert.False(provider.WindowManagerHandlesCloseButton);
    }
}
