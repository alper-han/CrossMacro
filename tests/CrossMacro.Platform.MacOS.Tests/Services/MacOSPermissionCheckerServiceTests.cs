using CrossMacro.Platform.MacOS.Services;
using System.Runtime.Versioning;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Services;

[SupportedOSPlatform("macos")]
public class MacOSPermissionCheckerServiceTests
{
    [Fact]
    public void IsSupported_ShouldAlwaysBeTrue()
    {
        var checker = new MacOSPermissionCheckerService();

        Assert.True(checker.IsSupported);
    }

    [Fact]
    public void RequiresStartupPermissionGate_ShouldBeTrue()
    {
        var checker = new MacOSPermissionCheckerService();

        Assert.True(checker.RequiresStartupPermissionGate);
    }

    [Fact]
    public void CheckUInputAccess_ShouldAlwaysReturnFalse()
    {
        var checker = new MacOSPermissionCheckerService();

        Assert.False(checker.CheckUInputAccess());
    }
}
