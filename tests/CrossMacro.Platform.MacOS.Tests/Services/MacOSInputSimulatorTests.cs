using CrossMacro.Platform.MacOS.Services;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services;

public class MacOSInputSimulatorTests
{
    [Fact]
    public void ProviderName_IsExpected()
    {
        var simulator = new MacOSInputSimulator();

        Assert.Equal("macOS CoreGraphics", simulator.ProviderName);
    }

    [Fact]
    public void IsSupported_MatchesCurrentPlatform()
    {
        var simulator = new MacOSInputSimulator();

        Assert.Equal(OperatingSystem.IsMacOS(), simulator.IsSupported);
    }
}
