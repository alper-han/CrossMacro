using CrossMacro.Core.Services;
using CrossMacro.Platform.Windows.Services;
using Xunit;

namespace CrossMacro.Platform.Windows.Tests.Services;

public class WindowsInputSimulatorTests
{
    [Fact]
    public void ProviderName_IsExpected()
    {
        var simulator = new WindowsInputSimulator();

        Assert.Equal("Windows SendInput", simulator.ProviderName);
    }

    [Fact]
    public void IsSupported_MatchesCurrentPlatform()
    {
        var simulator = new WindowsInputSimulator();

        Assert.Equal(OperatingSystem.IsWindows(), simulator.IsSupported);
    }

    [Fact]
    public void SupportsUnicodeTextInput_MatchesPlatformSupport()
    {
        var simulator = new WindowsInputSimulator();

        Assert.IsAssignableFrom<IUnicodeTextInputSimulator>(simulator);
        Assert.IsAssignableFrom<ITaggedKeyboardInputSimulator>(simulator);
        Assert.IsAssignableFrom<ITaggedUnicodeTextInputSimulator>(simulator);
        Assert.Equal(simulator.IsSupported, simulator.SupportsUnicodeTextInput);
        Assert.Equal(simulator.IsSupported, simulator.SupportsTaggedKeyboardInput);
    }
}
