using Avalonia.Controls.ApplicationLifetimes;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Services;
using NSubstitute;

namespace CrossMacro.UI.Tests.Services;

public sealed class DesktopPermissionGateServiceTests
{
    [Fact]
    public void IsStartupPermissionBlocked_WhenCheckerUnsupported_ReturnsFalse()
    {
        var checker = Substitute.For<IPermissionChecker>();
        checker.IsSupported.Returns(false);

        Assert.False(DesktopPermissionGateService.IsStartupPermissionBlocked(checker));
    }

    [Fact]
    public void IsStartupPermissionBlocked_WhenStartupGateNotRequired_ReturnsFalse()
    {
        var checker = Substitute.For<IPermissionChecker>();
        checker.IsSupported.Returns(true);
        checker.RequiresStartupPermissionGate.Returns(false);

        Assert.False(DesktopPermissionGateService.IsStartupPermissionBlocked(checker));
    }

    [Fact]
    public void IsStartupPermissionBlocked_WhenAccessibilityUntrusted_ReturnsTrue()
    {
        var checker = Substitute.For<IPermissionChecker>();
        checker.IsSupported.Returns(true);
        checker.RequiresStartupPermissionGate.Returns(true);
        checker.IsAccessibilityTrusted().Returns(false);

        Assert.True(DesktopPermissionGateService.IsStartupPermissionBlocked(checker));
    }

    [Fact]
    public async Task TryHandleAsync_WhenSessionUnsupported_ReturnsUnsupportedReasonWithoutHandling()
    {
        var displaySessionService = Substitute.For<IDisplaySessionService>();
        displaySessionService.IsSessionSupported(out Arg.Any<string>())
            .Returns(ci =>
            {
                ci[0] = "unsupported session";
                return false;
            });

        var service = new DesktopPermissionGateService(displaySessionService, () => null);
        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();

        var result = await service.TryHandleAsync(desktop);

        Assert.False(result.Handled);
        Assert.Equal("unsupported session", result.UnsupportedSessionReason);
    }
}
