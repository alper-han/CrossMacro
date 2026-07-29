
namespace CrossMacro.UI.Services;

internal sealed class DesktopStartupRuntimeService : IAsyncDisposable
{
    internal readonly record struct DesktopStartupDisplayPlan(
        DesktopStartupDisplayMode InitialDisplayMode,
        ShutdownMode ShutdownMode,
        bool ShowInTaskbar,
        bool ShowActivated,
        WindowState InitialState,
        bool ShouldDisableStartupOnlyTray);

    internal readonly record struct DesktopStartupUiResources(
        MainWindowViewModel MainWindowViewModel,
        MainWindow MainWindow,
        ITrayIconService TrayIconService);

    private readonly Func<MainWindow> _getMainWindow;
    private readonly Func<ITrayIconService> _getTrayIconService;
    private readonly Func<MainWindowViewModel> _getMainWindowViewModel;
    private readonly Func<IInputSimulatorPool?> _getInputSimulatorPool;
    private readonly Func<IMousePositionProvider?> _getPositionProvider;
    private readonly IDesktopLifetimeContext _desktopLifetimeContext;
    private readonly Func<CancellationToken, Task>? _screenReadingWarmup;
    private readonly IPortalScreenReadingGuidanceService? _portalScreenReadingGuidanceService;
    private readonly IRuntimeLifecycle _runtimeLifecycle;
    private readonly Func<Func<DesktopStartupUiResources>, Task<DesktopStartupUiResources>> _executeOnUiThread;
    private readonly CancellationTokenSource _warmupCancellation = new();
    private readonly List<Task> _warmupTasks = [];
    private int _stopped;

    public DesktopStartupRuntimeService(
        Func<MainWindow> getMainWindow,
        Func<ITrayIconService> getTrayIconService,
        Func<ITextExpansionService> getTextExpansionService,
        Func<MainWindowViewModel> getMainWindowViewModel,
        Func<IInputSimulatorPool?> getInputSimulatorPool,
        Func<IMousePositionProvider?> getPositionProvider,
        IDesktopLifetimeContext desktopLifetimeContext,
        Func<CancellationToken, Task>? screenReadingWarmup = null,
        IPortalScreenReadingGuidanceService? portalScreenReadingGuidanceService = null,
        IRuntimeLifecycle? runtimeLifecycle = null,
        Func<Func<DesktopStartupUiResources>, Task<DesktopStartupUiResources>>? executeOnUiThread = null)
    {
        _getMainWindow = getMainWindow ?? throw new ArgumentNullException(nameof(getMainWindow));
        _getTrayIconService = getTrayIconService ?? throw new ArgumentNullException(nameof(getTrayIconService));
        ArgumentNullException.ThrowIfNull(getTextExpansionService);
        _getMainWindowViewModel = getMainWindowViewModel ?? throw new ArgumentNullException(nameof(getMainWindowViewModel));
        _getInputSimulatorPool = getInputSimulatorPool ?? throw new ArgumentNullException(nameof(getInputSimulatorPool));
        _getPositionProvider = getPositionProvider ?? throw new ArgumentNullException(nameof(getPositionProvider));
        _desktopLifetimeContext = desktopLifetimeContext ?? throw new ArgumentNullException(nameof(desktopLifetimeContext));
        _screenReadingWarmup = screenReadingWarmup;
        _portalScreenReadingGuidanceService = portalScreenReadingGuidanceService;
        _runtimeLifecycle = runtimeLifecycle ?? CreateLifecycle(getTextExpansionService);
        _executeOnUiThread = executeOnUiThread ?? ExecuteOnUiThreadAsync;
    }

    private static async Task<DesktopStartupUiResources> ExecuteOnUiThreadAsync(
        Func<DesktopStartupUiResources> action)
    {
        return await Dispatcher.UIThread.InvokeAsync(action);
    }

    internal static IRuntimeLifecycle CreateLifecycle(Func<ITextExpansionService> getTextExpansionService)
    {
        ArgumentNullException.ThrowIfNull(getTextExpansionService);

        return new RuntimeLifecycle(
        [
            new RuntimeLifecycleStep("text expansion", cancellationToken =>
            {
                return getTextExpansionService().StartAsync(cancellationToken);
            }, cancellationToken =>
            {
                var textExpansionService = getTextExpansionService();
                if (textExpansionService.IsRunning)
                {
                    return textExpansionService.StopExpansionAsync(cancellationToken);
                }

                return Task.CompletedTask;
            }),
        ]);
    }

    public async Task StartAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var startupResources = await _executeOnUiThread(() =>
        {
            var mainWindowViewModel = _getMainWindowViewModel();
            var mainWindow = _getMainWindow();
            mainWindow.DataContext = mainWindowViewModel;

            var trayIconService = _getTrayIconService();
            PublishMainWindow(desktop, mainWindow);
            trayIconService.Initialize();
            return new DesktopStartupUiResources(mainWindowViewModel, mainWindow, trayIconService);
        }).ConfigureAwait(false);

        var inputSimulatorPool = _getInputSimulatorPool();
        if (inputSimulatorPool is not null)
        {
            _warmupTasks.Add(InputSimulatorWarmupService.WarmUpAsync(
                inputSimulatorPool,
                _getPositionProvider(),
                _warmupCancellation.Token));
        }

        await _runtimeLifecycle.StartAsync(CancellationToken.None).ConfigureAwait(true);

        startupResources = await _executeOnUiThread(() =>
        {
            startupResources.TrayIconService.SetEnabled(startupPreferences.ShouldEnableTrayDuringStartup);
            startupResources.MainWindowViewModel.TrayIconEnabledChanged +=
                (_, enabled) => startupResources.TrayIconService.SetEnabled(enabled);

            var displayMode = DesktopStartupRuntimeService.ConfigureMainWindow(
                desktop,
                startupResources.MainWindow,
                startupPreferences,
                startupResources.TrayIconService);
            ShowWindowForStartup(startupResources.MainWindow, displayMode);
            return startupResources;
        }).ConfigureAwait(false);

        if (_screenReadingWarmup is not null)
        {
            _warmupTasks.Add(RunScreenReadingWarmupAsync(_warmupCancellation.Token));
        }

    }

    internal async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) is not 0)
        {
            return;
        }

        var errors = new List<Exception>();
        try
        {
            await _runtimeLifecycle.StopAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }

        await _warmupCancellation.CancelAsync().ConfigureAwait(true);
        try
        {
            await Task.WhenAll(_warmupTasks).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { /* Empty */ }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }
        finally
        {
            _warmupCancellation.Dispose();
        }

        if (errors.Count > 0)
        {
            throw new AggregateException("Desktop runtime shutdown failed.", errors);
        }
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    internal Task RunScreenReadingWarmupAsync() => RunScreenReadingWarmupAsync(CancellationToken.None);

    private async Task RunScreenReadingWarmupAsync(CancellationToken cancellationToken)
    {
        if (_screenReadingWarmup is null)
        {
            return;
        }

        if (_portalScreenReadingGuidanceService is not null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _portalScreenReadingGuidanceService.ShowBeforePortalWarmupAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "[DesktopStartupRuntimeService] Portal screen-reading guidance failed; continuing warm-up");
            }
        }

        try
        {
            await _screenReadingWarmup(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[DesktopStartupRuntimeService] Portal screen-reading warm-up failed");
        }
    }

    internal void PublishMainWindow(IClassicDesktopStyleApplicationLifetime desktop, Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(mainWindow);

        if (!ReferenceEquals(_desktopLifetimeContext.DesktopLifetime, desktop))
        {
            _desktopLifetimeContext.Attach(desktop);
        }

        _desktopLifetimeContext.SetMainWindow(mainWindow);
    }

    internal static DesktopStartupDisplayMode ConfigureMainWindow(
        IClassicDesktopStyleApplicationLifetime desktop,
        Window mainWindow,
        DesktopStartupPreferences startupPreferences,
        ITrayIconService trayIconService)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(trayIconService);

        var plan = DesktopStartupRuntimeService.CreateDisplayPlan(startupPreferences, trayIconService.IsAvailable);

        mainWindow.ShowInTaskbar = plan.ShowInTaskbar;
        mainWindow.ShowActivated = plan.ShowActivated;
        mainWindow.WindowState = plan.InitialState;
        desktop.ShutdownMode = plan.ShutdownMode;

        if (plan.ShouldDisableStartupOnlyTray)
        {
            DisableStartupOnlyTrayAfterInitialRestore(mainWindow, trayIconService);
        }

        switch (plan.InitialDisplayMode)
        {
            case DesktopStartupDisplayMode.Visible:
                Log.Information("[DesktopStartupCoordinator] Started visible.");
                break;
            case DesktopStartupDisplayMode.Minimized:
                Log.Information("[DesktopStartupCoordinator] Started minimized.");
                break;
            case DesktopStartupDisplayMode.HiddenToTray:
                Log.Information("[DesktopStartupCoordinator] Started hidden to tray.");
                break;
        }

        return plan.InitialDisplayMode;
    }

    internal static DesktopStartupDisplayPlan CreateDisplayPlan(
        DesktopStartupPreferences startupPreferences,
        bool trayAvailable)
    {
        var displayMode = startupPreferences.ResolveDisplayMode(trayAvailable);

        return CreateDisplayPlan(displayMode, startupPreferences.UseStartupTrayOnly);
    }

    internal static DesktopStartupDisplayPlan CreateDisplayPlan(
        DesktopStartupDisplayMode displayMode,
        bool shouldDisableStartupOnlyTray)
    {
        return displayMode switch
        {
            DesktopStartupDisplayMode.Visible => new DesktopStartupDisplayPlan(
                InitialDisplayMode: displayMode,
                ShutdownMode: ShutdownMode.OnLastWindowClose,
                ShowInTaskbar: true,
                ShowActivated: true,
                InitialState: WindowState.Normal,
                ShouldDisableStartupOnlyTray: false),
            DesktopStartupDisplayMode.Minimized => new DesktopStartupDisplayPlan(
                InitialDisplayMode: displayMode,
                ShutdownMode: ShutdownMode.OnLastWindowClose,
                ShowInTaskbar: true,
                ShowActivated: false,
                InitialState: WindowState.Minimized,
                ShouldDisableStartupOnlyTray: false),
            DesktopStartupDisplayMode.HiddenToTray => new DesktopStartupDisplayPlan(
                InitialDisplayMode: displayMode,
                ShutdownMode: ShutdownMode.OnExplicitShutdown,
                ShowInTaskbar: false,
                ShowActivated: true,
                InitialState: WindowState.Normal,
                ShouldDisableStartupOnlyTray: shouldDisableStartupOnlyTray),
            _ => throw new ArgumentOutOfRangeException(nameof(displayMode), displayMode, "Unknown initial display mode."),
        };
    }

    private static void DisableStartupOnlyTrayAfterInitialRestore(Window mainWindow, ITrayIconService trayIconService)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(trayIconService);

        void OnOpened(object? sender, EventArgs e)
        {
            mainWindow.Opened -= OnOpened;
            trayIconService.SetEnabled(enabled: false);
            Log.Information("[DesktopStartupCoordinator] Disabled startup-only tray after initial restore.");
        }

        mainWindow.Opened += OnOpened;
    }

    private static void ShowWindowForStartup(Window mainWindow, DesktopStartupDisplayMode displayMode)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        switch (displayMode)
        {
            case DesktopStartupDisplayMode.HiddenToTray:
                return;
            case DesktopStartupDisplayMode.Minimized:
                if (!mainWindow.IsVisible)
                {
                    mainWindow.ShowActivated = false;
                    mainWindow.ShowInTaskbar = true;
                    mainWindow.Show();
                    mainWindow.ShowActivated = true;
                }
                return;
            case DesktopStartupDisplayMode.Visible:
                if (!mainWindow.IsVisible)
                {
                    mainWindow.Show();
                }
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(displayMode), displayMode, "Unknown initial display mode.");
        }
    }
}
