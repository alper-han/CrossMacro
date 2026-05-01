using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CrossMacro.UI.Services;
using CrossMacro.UI.Startup;
using NSubstitute;

namespace CrossMacro.UI.Tests.Services;

public sealed class DesktopQuickSetupGateServiceTests
{
    [Fact]
    public async Task TryHandleAsync_WhenNoUnsupportedReasonAndNoAppImagePrompt_ReturnsFalse()
    {
        var service = new DesktopQuickSetupGateService(
            getFlatpakQuickSetupService: () => null,
            getAppImageQuickSetupService: () => null);

        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();
        var started = false;

        var handled = await service.TryHandleAsync(
            desktop,
            new DesktopStartupPreferences(false, false, false),
            unsupportedSessionReason: null,
            startDesktopRuntimeAsync: (_, _) =>
            {
                started = true;
                return Task.CompletedTask;
            });

        Assert.False(handled);
        Assert.False(started);
    }

}
