
namespace CrossMacro.UI.Tests.Services;

public sealed class DesktopStartupRuntimeServiceTests
{
    [Fact]
    public async Task StartAsync_WhenWorkerOriginated_ResolvesMainWindowOutsideUiExecutionBoundary()
    {
        var sentinel = new InvalidOperationException(
            "MainWindow factory reached outside the expected Avalonia UI execution boundary.");
        var startupThreadId = 0;
        var uiExecutionThreadId = 0;
        var factoryThreadId = 0;
        var service = CreateService(
            getMainWindow: () =>
            {
                factoryThreadId = Environment.CurrentManagedThreadId;
                throw sentinel;
            },
            getMainWindowViewModel: () =>
                (MainWindowViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainWindowViewModel)),
            executeOnUiThread: action => Task.Run(() =>
            {
                uiExecutionThreadId = Environment.CurrentManagedThreadId;
                return action();
            }));
        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();
        var startupPreferences = new DesktopStartupPreferences(
            ShouldStartMinimized: false,
            PersistTrayEnabled: false,
            UseStartupTrayOnly: false);

        var exception = await Record.ExceptionAsync(
            () => Task.Factory.StartNew(
                () =>
                {
                    startupThreadId = Environment.CurrentManagedThreadId;
                    return service.StartAsync(desktop, startupPreferences);
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap());

        Assert.Same(sentinel, exception);
        Assert.NotEqual(startupThreadId, uiExecutionThreadId);
        Assert.Equal(uiExecutionThreadId, factoryThreadId);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotentAndDoesNotDisposeInjectedRuntimeLifecycle()
    {
        var runtimeLifecycle = Substitute.For<IRuntimeLifecycle>();
        var service = CreateService(runtimeLifecycle: runtimeLifecycle);

        await service.DisposeAsync();
        await service.DisposeAsync();
        await service.StopAsync();

        await runtimeLifecycle.Received(1).StopAsync(CancellationToken.None);
        await runtimeLifecycle.DidNotReceive().DisposeAsync();
    }

    [Fact]
    public void DisposeCreatedMainWindowViewModel_DoesNotResolveLazyViewModel()
    {
        var service = CreateService(
            getMainWindowViewModel: () => throw new InvalidOperationException("The view model must not be resolved during cleanup."));

        var exception = Record.Exception(service.DisposeCreatedMainWindowViewModel);

        Assert.Null(exception);
    }

    [Fact]
    public async Task CleanupAsync_AttemptsRuntimeViewModelAndProviderIndependently()
    {
        var events = new List<string>();

        var error = await App.CleanupAsync(
            () =>
            {
                events.Add("runtime");
                throw new InvalidOperationException("runtime stop failed");
            },
            () =>
            {
                events.Add("view-model");
                throw new InvalidOperationException("view-model dispose failed");
            },
            () =>
            {
                events.Add("provider");
                return Task.FromException(new InvalidOperationException("provider dispose failed"));
            });

        Assert.Equal(["runtime", "view-model", "provider"], events);
        Assert.NotNull(error);
        Assert.Equal(3, error!.InnerExceptions.Count);
    }

    [Fact]
    public async Task CleanupAsync_AwaitsAsyncProviderDisposalBeforeCompleting()
    {
        var providerDisposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerDisposeCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var cleanup = App.CleanupAsync(
            () => Task.CompletedTask,
            static () => { },
            async () =>
            {
                providerDisposeStarted.SetResult();
                await providerDisposeCompleted.Task;
            });

        await providerDisposeStarted.Task;

        Assert.False(cleanup.IsCompleted);

        providerDisposeCompleted.SetResult();
        Assert.Null(await cleanup);
    }

    [Fact]
    public void CreateDisplayPlan_WhenDisplayModeIsVisible_UsesNormalWindowWithLastWindowClose()
    {
        var preferences = new DesktopStartupPreferences(
            ShouldStartMinimized: false,
            PersistTrayEnabled: false,
            UseStartupTrayOnly: false);

        var plan = DesktopStartupRuntimeService.CreateDisplayPlan(preferences, trayAvailable: true);

        Assert.Equal(DesktopStartupDisplayMode.Visible, plan.InitialDisplayMode);
        Assert.Equal(ShutdownMode.OnLastWindowClose, plan.ShutdownMode);
        Assert.True(plan.ShowInTaskbar);
        Assert.True(plan.ShowActivated);
        Assert.Equal(WindowState.Normal, plan.InitialState);
        Assert.False(plan.ShouldDisableStartupOnlyTray);
    }

    [Fact]
    public void CreateDisplayPlan_WhenTrayUnavailable_StartsMinimizedWindow()
    {
        var preferences = new DesktopStartupPreferences(
            ShouldStartMinimized: true,
            PersistTrayEnabled: true,
            UseStartupTrayOnly: false);

        var plan = DesktopStartupRuntimeService.CreateDisplayPlan(preferences, trayAvailable: false);

        Assert.Equal(DesktopStartupDisplayMode.Minimized, plan.InitialDisplayMode);
        Assert.Equal(ShutdownMode.OnLastWindowClose, plan.ShutdownMode);
        Assert.False(plan.ShowActivated);
        Assert.True(plan.ShowInTaskbar);
        Assert.Equal(WindowState.Minimized, plan.InitialState);
        Assert.False(plan.ShouldDisableStartupOnlyTray);
    }

    [Fact]
    public void CreateDisplayPlan_WhenTrayAvailableAndStartupTrayOnly_HidesToTrayAndDisablesTrayAfterRestore()
    {
        var preferences = new DesktopStartupPreferences(
            ShouldStartMinimized: true,
            PersistTrayEnabled: false,
            UseStartupTrayOnly: true);

        var plan = DesktopStartupRuntimeService.CreateDisplayPlan(preferences, trayAvailable: true);

        Assert.Equal(DesktopStartupDisplayMode.HiddenToTray, plan.InitialDisplayMode);
        Assert.Equal(ShutdownMode.OnExplicitShutdown, plan.ShutdownMode);
        Assert.False(plan.ShowInTaskbar);
        Assert.True(plan.ShowActivated);
        Assert.Equal(WindowState.Normal, plan.InitialState);
        Assert.True(plan.ShouldDisableStartupOnlyTray);
    }

    [Fact]
    public void CreateDisplayPlan_WhenTrayAvailableAndPersistedTrayEnabled_KeepsTrayEnabledAfterRestore()
    {
        var preferences = new DesktopStartupPreferences(
            ShouldStartMinimized: true,
            PersistTrayEnabled: true,
            UseStartupTrayOnly: false);

        var plan = DesktopStartupRuntimeService.CreateDisplayPlan(preferences, trayAvailable: true);

        Assert.Equal(DesktopStartupDisplayMode.HiddenToTray, plan.InitialDisplayMode);
        Assert.Equal(ShutdownMode.OnExplicitShutdown, plan.ShutdownMode);
        Assert.False(plan.ShowInTaskbar);
        Assert.False(plan.ShouldDisableStartupOnlyTray);
    }

    [Fact]
    public void CreateDisplayPlan_WhenDisplayModeIsInvalid_ReportsDisplayModeParameter()
    {
        var displayMode = (DesktopStartupDisplayMode)999;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => DesktopStartupRuntimeService.CreateDisplayPlan(
                displayMode,
                shouldDisableStartupOnlyTray: false));

        Assert.Equal("displayMode", exception.ParamName);
        Assert.Equal(displayMode, exception.ActualValue);
    }

    [Fact]
    public void PublishMainWindow_WhenDesktopContextWasAttachedBeforeWindowExists_SynchronizesContextAndDesktop()
    {
        var context = new DesktopLifetimeContext();
        var desktop = Substitute.For<IClassicDesktopStyleApplicationLifetime>();
        var mainWindow = CreateWindowReferenceOnly();
        var service = CreateService(context);

        context.Attach(desktop);
        service.PublishMainWindow(desktop, mainWindow);

        Assert.Same(desktop, context.DesktopLifetime);
        Assert.Same(mainWindow, context.MainWindow);
        desktop.Received().MainWindow = mainWindow;
    }

    [Fact]
    public async Task RunScreenReadingWarmupAsync_WhenGuidanceRegistered_AwaitsGuidanceBeforeWarmup()
    {
        var events = new List<string>();
        var guidance = new RecordingGuidanceService(events);
        var warmup = new RecordingWarmupService(events);
        var service = CreateService(screenReadingWarmup: warmup.WarmUpPortalSessionAsync, portalScreenReadingGuidanceService: guidance);

        await service.RunScreenReadingWarmupAsync();

        Assert.Equal(["guidance-start", "guidance-end", "warmup"], events);
    }

    [Fact]
    public async Task RunScreenReadingWarmupAsync_WhenGuidanceThrows_StillRunsWarmup()
    {
        var events = new List<string>();
        var guidance = new RecordingGuidanceService(events) { ThrowOnShow = true };
        var warmup = new RecordingWarmupService(events);
        var service = CreateService(screenReadingWarmup: warmup.WarmUpPortalSessionAsync, portalScreenReadingGuidanceService: guidance);

        await service.RunScreenReadingWarmupAsync();

        Assert.Equal(["guidance-start", "warmup"], events);
    }

    [Fact]
    public async Task RunScreenReadingWarmupAsync_WhenNoGuidanceRegistered_StillRunsWarmup()
    {
        var events = new List<string>();
        var warmup = new RecordingWarmupService(events);
        var service = CreateService(screenReadingWarmup: warmup.WarmUpPortalSessionAsync);

        await service.RunScreenReadingWarmupAsync();

        Assert.Equal(["warmup"], events);
    }

    [Fact]
    public async Task RunScreenReadingWarmupAsync_WhenWarmupThrows_CompletesWithoutThrowing()
    {
        var events = new List<string>();
        var warmup = new RecordingWarmupService(events) { ThrowOnWarmup = true };
        var service = CreateService(screenReadingWarmup: warmup.WarmUpPortalSessionAsync);

        await service.RunScreenReadingWarmupAsync();

        Assert.Equal(["warmup"], events);
    }

    private static Window CreateWindowReferenceOnly()
    {
        // The test only verifies lifetime reference synchronization; constructing an Avalonia Window requires a windowing platform.
        return (Window)RuntimeHelpers.GetUninitializedObject(typeof(Window));
    }

    private static DesktopStartupRuntimeService CreateService(
        IDesktopLifetimeContext? desktopLifetimeContext = null,
        Func<CancellationToken, Task>? screenReadingWarmup = null,
        IPortalScreenReadingGuidanceService? portalScreenReadingGuidanceService = null,
        IRuntimeLifecycle? runtimeLifecycle = null,
        Func<CrossMacro.UI.Views.MainWindow>? getMainWindow = null,
        Func<MainWindowViewModel>? getMainWindowViewModel = null,
        Func<Func<DesktopStartupRuntimeService.DesktopStartupUiResources>, Task<DesktopStartupRuntimeService.DesktopStartupUiResources>>? executeOnUiThread = null)
    {
        return new DesktopStartupRuntimeService(
            getMainWindow: getMainWindow ?? (() => throw new NotSupportedException()),
            getTrayIconService: () => new FakeTrayIconService(),
            getTextExpansionService: () => Substitute.For<ITextExpansionService>(),
            getMainWindowViewModel: getMainWindowViewModel ?? (() => throw new NotSupportedException()),
            getInputSimulatorPool: () => null,
            getPositionProvider: () => null,
            desktopLifetimeContext: desktopLifetimeContext ?? Substitute.For<IDesktopLifetimeContext>(),
            screenReadingWarmup: screenReadingWarmup,
            portalScreenReadingGuidanceService: portalScreenReadingGuidanceService,
            runtimeLifecycle: runtimeLifecycle,
            executeOnUiThread: executeOnUiThread);
    }

    private sealed class RecordingGuidanceService(List<string> events) : IPortalScreenReadingGuidanceService
    {
        private readonly List<string> _events = events;

        public bool ThrowOnShow { get; init; }

        public Task ShowBeforePortalWarmupAsync()
        {
            _events.Add("guidance-start");

            if (ThrowOnShow)
            {
                throw new InvalidOperationException("guidance failed");
            }

            _events.Add("guidance-end");
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingWarmupService(List<string> events)
    {
        private readonly List<string> _events = events;

        public bool ThrowOnWarmup { get; init; }

        public Task WarmUpPortalSessionAsync(CancellationToken cancellationToken)
        {
            _events.Add("warmup");
            if (ThrowOnWarmup)
            {
                throw new InvalidOperationException("warmup failed");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeTrayIconService : ITrayIconService
    {
        public bool IsAvailable { get; set; }

        public List<bool> EnabledCalls { get; } = [];

        public void Initialize()
        {
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void UpdateTooltip(string tooltip)
        {
        }

        public void SetEnabled(bool enabled)
        {
            EnabledCalls.Add(enabled);
        }

        public void Dispose()
        {
        }
    }
}
