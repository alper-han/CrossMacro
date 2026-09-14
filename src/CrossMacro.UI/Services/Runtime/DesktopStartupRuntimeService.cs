
namespace CrossMacro.UI.Services.Runtime;

internal sealed class DesktopStartupRuntimeService(
        Func<MainWindow> getMainWindow,
        Func<ITrayIconService> getTrayIconService,
        Func<MainWindowViewModel> getMainWindowViewModel,
        Func<IInputSimulatorPool?> getInputSimulatorPool,
        Func<IMousePositionProvider?> getPositionProvider,
        IDesktopLifetimeContext desktopLifetimeContext,
        IRuntimeLifecycle runtimeLifecycle,
        Func<CancellationToken, Task>? screenReadingWarmup = null,
        IPortalScreenReadingGuidanceService? portalScreenReadingGuidanceService = null,
        Func<Func<DesktopStartupRuntimeService.DesktopStartupUiResources>, Task<DesktopStartupRuntimeService.DesktopStartupUiResources>>? executeOnUiThread = null,
        IUiDispatcher? uiDispatcher = null) : IAsyncDisposable
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

    private readonly Func<MainWindow> _getMainWindow = getMainWindow ?? throw new ArgumentNullException(nameof(getMainWindow));
    private readonly Func<ITrayIconService> _getTrayIconService = getTrayIconService ?? throw new ArgumentNullException(nameof(getTrayIconService));
    private readonly Func<MainWindowViewModel> _getMainWindowViewModel = getMainWindowViewModel ?? throw new ArgumentNullException(nameof(getMainWindowViewModel));
    private readonly Func<IInputSimulatorPool?> _getInputSimulatorPool = getInputSimulatorPool ?? throw new ArgumentNullException(nameof(getInputSimulatorPool));
    private readonly Func<IMousePositionProvider?> _getPositionProvider = getPositionProvider ?? throw new ArgumentNullException(nameof(getPositionProvider));
    private readonly IDesktopLifetimeContext _desktopLifetimeContext = desktopLifetimeContext ?? throw new ArgumentNullException(nameof(desktopLifetimeContext));
    private readonly Func<CancellationToken, Task>? _screenReadingWarmup = screenReadingWarmup;
    private readonly IPortalScreenReadingGuidanceService? _portalScreenReadingGuidanceService = portalScreenReadingGuidanceService;
    private readonly IRuntimeLifecycle _runtimeLifecycle = runtimeLifecycle ?? throw new ArgumentNullException(nameof(runtimeLifecycle));
    private readonly Func<Func<DesktopStartupUiResources>, Task<DesktopStartupUiResources>> _executeOnUiThread = executeOnUiThread ?? (action => (uiDispatcher ?? AvaloniaUiDispatcher.Instance).InvokeAsync(action));
    private readonly CancellationTokenSource _warmupCancellation = new();
    private readonly List<Task> _warmupTasks = [];
    private readonly TaskCompletionSource _startupCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _startupStarted;
    private readonly Lock _stopGate = new();
    private Task? _stopTask;
    private Action? _unsubscribeTray;


    public async Task StartAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        lock (_stopGate)
        {
            if (_stopTask is not null) { throw new OperationCanceledException(new CancellationToken(canceled: true)); }
            if (_startupStarted is not 0) { return; }
            _startupStarted = 1;
        }

        try
        {
            _warmupCancellation.Token.ThrowIfCancellationRequested();

            var startupResources = await _executeOnUiThread(() =>
            {
                _warmupCancellation.Token.ThrowIfCancellationRequested();
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

            await startupResources.MainWindowViewModel.InitializeAsync().ConfigureAwait(false);
            await _runtimeLifecycle.StartAsync(_warmupCancellation.Token).ConfigureAwait(true);
            _warmupCancellation.Token.ThrowIfCancellationRequested();

            startupResources = await _executeOnUiThread(() =>
            {
                _warmupCancellation.Token.ThrowIfCancellationRequested();
                startupResources.TrayIconService.SetEnabled(startupPreferences.ShouldEnableTrayDuringStartup);
                void OnTrayChanged(object? sender, bool enabled) => startupResources.TrayIconService.SetEnabled(enabled);
                startupResources.MainWindowViewModel.TrayIconEnabledChanged += OnTrayChanged;
                _unsubscribeTray = () => startupResources.MainWindowViewModel.TrayIconEnabledChanged -= OnTrayChanged;

                var displayMode = DesktopStartupRuntimeService.ConfigureMainWindow(
                    desktop,
                    startupResources.MainWindow,
                    startupPreferences,
                    startupResources.TrayIconService);
                ShowWindowForStartup(startupResources.MainWindow, displayMode);
                return startupResources;
            }).ConfigureAwait(false);

            _warmupTasks.Add(startupResources.MainWindowViewModel.StartOptionalBackgroundWorkAsync(_warmupCancellation.Token));

            if (_screenReadingWarmup is not null)
            {
                _warmupTasks.Add(RunScreenReadingWarmupAsync(_warmupCancellation.Token));
            }
        }
        finally
        {
            _ = _startupCompletion.TrySetResult();
        }
    }

    internal Task StopAsync()
    {
        lock (_stopGate)
        {
            return _stopTask ??= StopCoreAsync();
        }
    }

    private async Task StopCoreAsync()
    {

        var errors = new List<Exception>();

        try
        {
            await _warmupCancellation.CancelAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }

        if (Volatile.Read(ref _startupStarted) is 0)
        {
            _ = _startupCompletion.TrySetResult();
        }

        try
        {
            await _startupCompletion.Task.ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }

        try
        {
            Interlocked.Exchange(ref _unsubscribeTray, value: null)?.Invoke();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }

        try
        {
            await _runtimeLifecycle.StopAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }

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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown cancellation is an expected completion path.
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
