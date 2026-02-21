using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Services;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services;

public class MacOSEnvironmentInfoProviderTests
{
    [Fact]
    public void CurrentEnvironment_ReturnsMacOS()
    {
        var provider = new MacOSEnvironmentInfoProvider();

        Assert.Equal(DisplayEnvironment.MacOS, provider.CurrentEnvironment);
    }

    [Fact]
    public void WindowManagerHandlesCloseButton_ReturnsFalse()
    {
        var provider = new MacOSEnvironmentInfoProvider();

        Assert.False(provider.WindowManagerHandlesCloseButton);
    }
}
