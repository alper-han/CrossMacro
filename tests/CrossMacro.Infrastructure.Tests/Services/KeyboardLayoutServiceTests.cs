using CrossMacro.Infrastructure.Services;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class KeyboardLayoutServiceTests
{
    private readonly KeyboardLayoutService _service;

    public KeyboardLayoutServiceTests()
    {
        // On non-Linux (e.g. CI environments without X) this might log errors but should not throw.
        // We rely on fallback logic which is what we are testing here mainly.
        _service = new KeyboardLayoutService();
    }

    [Fact]
    public void GetKeyName_ReturnsCorrectFallback_ForStandardKeys()
    {
        // Assert
        Assert.Equal("A", _service.GetKeyName(30)); // A
        Assert.Equal("Space", _service.GetKeyName(57)); // Space
        Assert.Equal("Enter", _service.GetKeyName(28)); // Enter
    }

    [Fact]
    public void GetKeyCode_ReturnsCorrectFallback_ForStandardNames()
    {
        // Assert
        Assert.Equal(30, _service.GetKeyCode("A"));
        Assert.Equal(57, _service.GetKeyCode("Space"));
        Assert.Equal(29, _service.GetKeyCode("Ctrl"));
    }
}
