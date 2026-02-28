using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class HeadlessRuntimeServiceTests
{
    [Fact]
    public async Task RunAsync_WhenCancelledAfterStart_StopsServicesAndReturnsCancelled()
    {
        var display = Substitute.For<IDisplaySessionService>();
        display.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = string.Empty;
            return true;
        });

        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings());

        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var scheduler = Substitute.For<ISchedulerService>();
        var shortcuts = Substitute.For<IShortcutService>();
        var textExpansion = Substitute.For<ITextExpansionService>();
        var hotkeyActions = Substitute.For<IHeadlessHotkeyActionService>();
        textExpansion.IsRunning.Returns(true);
        hotkeyActions.IsRunning.Returns(true);

        var service = new HeadlessRuntimeService(display, settings, hotkeys, scheduler, shortcuts, textExpansion, hotkeyActions);

        using var cts = new CancellationTokenSource(10);
        var result = await service.RunAsync(cts.Token);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.Cancelled, result.ExitCode);
        hotkeys.Received(1).Start();
        scheduler.Received(1).Start();
        shortcuts.Received(1).Start();
        textExpansion.Received(1).Start();
        hotkeyActions.Received(1).Start();
        hotkeys.Received(1).Stop();
        scheduler.Received(1).Stop();
        shortcuts.Received(1).Stop();
        textExpansion.Received(1).Stop();
        hotkeyActions.Received(1).Stop();
    }

    [Fact]
    public async Task RunAsync_WhenDisplayUnsupported_ReturnsEnvironmentError()
    {
        var display = Substitute.For<IDisplaySessionService>();
        display.IsSessionSupported(out Arg.Any<string>()).Returns(x =>
        {
            x[0] = "unsupported";
            return false;
        });

        var settings = Substitute.For<ISettingsService>();
        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var scheduler = Substitute.For<ISchedulerService>();
        var shortcuts = Substitute.For<IShortcutService>();
        var textExpansion = Substitute.For<ITextExpansionService>();
        var hotkeyActions = Substitute.For<IHeadlessHotkeyActionService>();

        var service = new HeadlessRuntimeService(display, settings, hotkeys, scheduler, shortcuts, textExpansion, hotkeyActions);
        var result = await service.RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        hotkeys.DidNotReceive().Start();
    }
}
