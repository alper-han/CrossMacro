
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
            new DesktopStartupPreferences(ShouldStartMinimized: false, PersistTrayEnabled: false, UseStartupTrayOnly: false),
            unsupportedSessionReason: null,
            startDesktopRuntimeAsync: (_, _) =>
            {
                started = true;
                return Task.CompletedTask;
            });

        Assert.False(handled);
        Assert.False(started);
    }

    [Fact]
    public async Task TryHandleAsync_WhenOptionalProvidersDoNotPrompt_ReturnsFalse()
    {
        var appImage = Substitute.For<CrossMacro.Platform.Abstractions.Setup.IAppImageQuickSetupService>();
        _ = appImage.ShouldPrompt().Returns(returnThis: false);
        var directInput = Substitute.For<CrossMacro.Platform.Abstractions.Setup.ILinuxDirectInputQuickSetupService>();
        _ = directInput.ShouldPromptAsync(Arg.Any<CancellationToken>())
            .Returns(returnThis: new ValueTask<bool>(result: false));
        var service = new DesktopQuickSetupGateService(
            getFlatpakQuickSetupService: () => null,
            getAppImageQuickSetupService: () => appImage,
            getLinuxDirectInputQuickSetupService: () => directInput);

        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();
        var started = false;

        var handled = await service.TryHandleAsync(
            desktop,
            new DesktopStartupPreferences(ShouldStartMinimized: false, PersistTrayEnabled: false, UseStartupTrayOnly: false),
            unsupportedSessionReason: null,
            startDesktopRuntimeAsync: (_, _) =>
            {
                started = true;
                return Task.CompletedTask;
            });

        Assert.False(handled);
        Assert.False(started);
        _ = appImage.Received(1).ShouldPrompt();
        _ = directInput.Received(1).ShouldPromptAsync(Arg.Any<CancellationToken>());
    }

}
