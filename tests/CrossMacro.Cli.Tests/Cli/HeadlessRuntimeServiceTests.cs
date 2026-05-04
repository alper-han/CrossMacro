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
        var hotkeyActionsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hotkeyActionsStopped = false;
        textExpansion.IsRunning.Returns(true);
        hotkeyActions.IsRunning.Returns(true);
        hotkeyActions.When(x => x.Start()).Do(_ => hotkeyActionsStarted.TrySetResult());
        hotkeyActions.StopAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            hotkeyActionsStopped = true;
            return Task.CompletedTask;
        });

        var service = new HeadlessRuntimeService(display, settings, hotkeys, scheduler, shortcuts, textExpansion, hotkeyActions);

        using var cts = new CancellationTokenSource();
        var runTask = service.RunAsync(cts.Token);
        await hotkeyActionsStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cts.CancelAsync();
        var result = await runTask;

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
        await hotkeyActions.Received(1).StopAsync(Arg.Any<CancellationToken>());
        Assert.True(hotkeyActionsStopped);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledAfterStart_AwaitsHotkeyActionStopBeforeStoppingGlobalHotkeys()
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
        var hotkeyActionsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowStopToComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var globalHotkeysStoppedBeforeHotkeyActions = false;

        textExpansion.IsRunning.Returns(true);
        hotkeyActions.IsRunning.Returns(true);
        hotkeyActions.When(x => x.Start()).Do(_ => hotkeyActionsStarted.SetResult());
        hotkeyActions.StopAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            stopEntered.SetResult();
            await allowStopToComplete.Task;
        });
        hotkeys.When(x => x.Stop()).Do(_ =>
        {
            if (!allowStopToComplete.Task.IsCompleted)
            {
                globalHotkeysStoppedBeforeHotkeyActions = true;
            }
        });

        var service = new HeadlessRuntimeService(display, settings, hotkeys, scheduler, shortcuts, textExpansion, hotkeyActions);

        using var cts = new CancellationTokenSource();
        var runTask = service.RunAsync(cts.Token);
        await hotkeyActionsStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cts.CancelAsync();
        await stopEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(globalHotkeysStoppedBeforeHotkeyActions);

        allowStopToComplete.SetResult();
        var result = await runTask;

        Assert.Equal(CliExitCode.Cancelled, result.ExitCode);
        Assert.False(globalHotkeysStoppedBeforeHotkeyActions);
        hotkeys.Received(1).Stop();
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
