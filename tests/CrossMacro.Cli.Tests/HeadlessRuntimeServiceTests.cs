
namespace CrossMacro.Cli.Tests;

public sealed class HeadlessRuntimeServiceTests
{
    [Fact]
    public async Task RunAsync_WhenCancelledAfterStart_StopsServicesAndReturnsCancelled()
    {
        var display = new FakeDisplaySessionService(supported: true, reason: string.Empty);

        var settings = Substitute.For<ISettingsService>();
        _ = settings.LoadAsync().Returns(Task.FromResult(new AppSettings()));

        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var scheduler = Substitute.For<ISchedulerService>();
        var shortcuts = Substitute.For<IShortcutService>();
        var textExpansion = Substitute.For<ITextExpansionService>();
        var hotkeyActions = Substitute.For<IHeadlessHotkeyActionService>();
        var hotkeyActionsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var textExpansionStartEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowTextExpansionStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hotkeyActionsStopped = false;
        _ = textExpansion.IsRunning.Returns(returnThis: true);
        _ = hotkeyActions.IsRunning.Returns(returnThis: true);
        hotkeyActions.When(x => x.Start()).Do(_ => hotkeyActionsStarted.TrySetResult());
        _ = textExpansion.StartAsync(Arg.Any<CancellationToken>()).Returns(async unusedCallInfo =>
        {
            _ = textExpansionStartEntered.TrySetResult();
            await allowTextExpansionStart.Task;
        });
        _ = hotkeyActions.StopAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            hotkeyActionsStopped = true;
            return Task.CompletedTask;
        });

        var service = new HeadlessRuntimeService(display, settings, hotkeys, scheduler, shortcuts, textExpansion, hotkeyActions);

        using var cts = new CancellationTokenSource();
        var runTask = service.RunAsync(cts.Token);
        await textExpansionStartEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(hotkeyActionsStarted.Task.IsCompleted);
        allowTextExpansionStart.SetResult();
        await hotkeyActionsStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cts.CancelAsync();
        var result = await runTask;

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.Cancelled, result.ExitCode);
        hotkeys.Received(1).Start();
        scheduler.Received(1).Start();
        shortcuts.Received(1).Start();
        await textExpansion.Received(1).StartAsync(Arg.Any<CancellationToken>());
        hotkeyActions.Received(1).Start();
        await hotkeys.Received(1).StopHotkeyServiceAsync(Arg.Any<CancellationToken>());
        await scheduler.Received(1).StopAsync(Arg.Any<CancellationToken>());
        shortcuts.Received(1).StopShortcuts();
        await textExpansion.Received(1).StopExpansionAsync(Arg.Any<CancellationToken>());
        await hotkeyActions.Received(1).StopAsync(Arg.Any<CancellationToken>());
        Assert.True(hotkeyActionsStopped);
    }

    [Fact]
    public async Task RunAsync_WhenStarted_WarmsUpScreenReadingSession()
    {
        var display = new FakeDisplaySessionService(supported: true, reason: string.Empty);

        var settings = Substitute.For<ISettingsService>();
        _ = settings.LoadAsync().Returns(Task.FromResult(new AppSettings()));

        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var scheduler = Substitute.For<ISchedulerService>();
        var shortcuts = Substitute.For<IShortcutService>();
        var textExpansion = Substitute.For<ITextExpansionService>();
        var hotkeyActions = Substitute.For<IHeadlessHotkeyActionService>();
        var warmup = Substitute.For<CrossMacroPlatformWarmupService>();
        var warmupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _ = textExpansion.IsRunning.Returns(returnThis: true);
        _ = hotkeyActions.IsRunning.Returns(returnThis: true);
        _ = warmup.WarmUpPortalSessionAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        warmup.When(x => x.WarmUpPortalSessionAsync(Arg.Any<CancellationToken>()))
            .Do(_ => warmupStarted.TrySetResult());

        var service = new HeadlessRuntimeService(display, settings, hotkeys, scheduler, shortcuts, textExpansion, hotkeyActions, warmup);

        using var cts = new CancellationTokenSource();
        var runTask = service.RunAsync(cts.Token);
        await warmupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cts.CancelAsync();
        _ = await runTask;

        await warmup.Received(1).WarmUpPortalSessionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenCancelledAfterStart_AwaitsHotkeyActionStopBeforeStoppingGlobalHotkeys()
    {
        var display = new FakeDisplaySessionService(supported: true, reason: string.Empty);

        var settings = Substitute.For<ISettingsService>();
        _ = settings.LoadAsync().Returns(Task.FromResult(new AppSettings()));

        var hotkeys = Substitute.For<IGlobalHotkeyService>();
        var scheduler = Substitute.For<ISchedulerService>();
        var shortcuts = Substitute.For<IShortcutService>();
        var textExpansion = Substitute.For<ITextExpansionService>();
        var hotkeyActions = Substitute.For<IHeadlessHotkeyActionService>();
        var hotkeyActionsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowStopToComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var globalHotkeysStoppedBeforeHotkeyActions = false;

        _ = textExpansion.IsRunning.Returns(returnThis: true);
        _ = hotkeyActions.IsRunning.Returns(returnThis: true);
        hotkeyActions.When(x => x.Start()).Do(_ => hotkeyActionsStarted.SetResult());
        _ = hotkeyActions.StopAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            stopEntered.SetResult();
            await allowStopToComplete.Task;
        });
        hotkeys.When(x => x.StopHotkeyServiceAsync(Arg.Any<CancellationToken>())).Do(_ =>
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
        await hotkeys.Received(1).StopHotkeyServiceAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenDisplayUnsupported_ReturnsEnvironmentError()
    {
        var display = new FakeDisplaySessionService(supported: false, reason: "unsupported");

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
