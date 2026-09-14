
namespace CrossMacro.UI;

public class App : Avalonia.Application, IAsyncDisposable
{
    private readonly GuiBootstrapContext? _bootstrapContext;
    private readonly DesktopStartupLifetime _startupLifetime = new();
    private bool _shutdownStarted;
    private bool _shutdownCompleted;
    private readonly Lock _cleanupGate = new();
    private Task? _cleanupTask;
    private int _disposing;
    private SingleInstanceActivationListener? _activationListener;

    public App() { /* Empty */ }

    internal App(GuiBootstrapContext bootstrapContext)
    {
        _bootstrapContext = bootstrapContext ?? throw new ArgumentNullException(nameof(bootstrapContext));
    }


    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        Name = "CrossMacro";
        AvaloniaXamlLoader.Load(this);
        InitializeThemeResources();
        ConfigureServices();
    }

    private void InitializeThemeResources()
    {
        if (Resources is null)
        {
            return;
        }

        ThemeResourceDictionaryFactory.ReplaceActiveTheme(Resources, ThemeResourceDictionaryFactory.Create(ThemeCatalog.DefaultTheme));
    }

    private void ConfigureServices()
    {
        if (_bootstrapContext is null)
        {
            // Allow tooling/design-time hosts to construct App without a platform host project.
            Services = new ServiceCollection().BuildServiceProvider();
            return;
        }

        var services = new ServiceCollection();
        _ = services.AddSingleton(_bootstrapContext.StartupOptions);
        _bootstrapContext.ConfigureServices(services);
        _bootstrapContext.ConfigureRuntimeServices(services);
        _ = services.AddCrossMacroServices();
        Services = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            if (!Design.IsDesignMode && _bootstrapContext is null)
            {
                throw new InvalidOperationException(
                    "Platform service composition is not configured. Start the app via a platform host project.");
            }


            if (Services is null)
            {
                throw new InvalidOperationException("Service provider is not initialized");
            }

            AttachDesktopLifetime(desktopLifetime);
            desktopLifetime.ShutdownRequested += OnShutdownRequested;
            QueueDesktopStartup(desktopLifetime);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void AttachDesktopLifetime(IClassicDesktopStyleApplicationLifetime desktopLifetime)
    {
        var context = Services?.GetService<IDesktopLifetimeContext>();
        context?.Attach(desktopLifetime);
    }

    private void QueueDesktopStartup(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var startupCoordinator = GetDesktopStartupCoordinator();
            Dispatcher.UIThread.Post(
                () => _ = RunStartupAsync(startupCoordinator, desktop),
                DispatcherPriority.Send);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SerilogLog.Error(ex, "Desktop startup initialization failed");
            // Already on the UI thread; blocking on InvokeAsync would deadlock the dispatcher.
            desktop.Shutdown(1);
        }
    }

    private IDesktopStartupCoordinator GetDesktopStartupCoordinator()
    {
        var services = Services
            ?? throw new InvalidOperationException("Service provider is not initialized.");

        return services.GetRequiredService<IDesktopStartupCoordinator>();
    }

    private async Task RunStartupAsync(
        IDesktopStartupCoordinator startupCoordinator,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (Volatile.Read(ref _shutdownStarted) || Volatile.Read(ref _disposing) is not 0)
        {
            return;
        }

        try
        {
            await _startupLifetime.StartAsync(token => startupCoordinator.StartAsync(desktop, token)).ConfigureAwait(false);
            if (!Volatile.Read(ref _shutdownStarted) && Volatile.Read(ref _disposing) is 0)
            {
                var listener = Program.StartRuntimeActivationListener(ActivateMainWindow);
                Interlocked.Exchange(ref _activationListener, listener)?.Dispose();
                if (Volatile.Read(ref _shutdownStarted) || Volatile.Read(ref _disposing) is not 0)
                {
                    Interlocked.Exchange(ref _activationListener, value: null)?.Dispose();
                }
            }
        }
        catch (OperationCanceledException) when (Volatile.Read(ref _shutdownStarted))
        {
            // Shutdown cancellation is an expected completion path.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SerilogLog.Error(ex, "Desktop startup failed");
            await Dispatcher.UIThread.InvokeAsync(() => desktop.Shutdown(1), DispatcherPriority.Send, CancellationToken.None);
        }
    }

    private void ActivateMainWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            var mainWindow = desktop.MainWindow ?? Services?.GetService<IDesktopLifetimeContext>()?.MainWindow;
            if (mainWindow is null)
            {
                return;
            }

            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

            if (!mainWindow.IsVisible)
            {
                mainWindow.Show();
            }

            if (mainWindow.WindowState is WindowState.Minimized)
            {
                mainWindow.WindowState = WindowState.Normal;
            }

            mainWindow.Activate();
            mainWindow.BringIntoView();
        }, DispatcherPriority.Send);
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        e.Cancel = true;
        if (Volatile.Read(ref _shutdownStarted) || Volatile.Read(ref _disposing) is not 0)
        {
            return;
        }

        Volatile.Write(ref _shutdownStarted, value: true);
        _ = CompleteShutdownAsync((IClassicDesktopStyleApplicationLifetime)sender!);
    }

    private async Task CompleteShutdownAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try { await DisposeAsync().ConfigureAwait(true); }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SerilogLog.Error(exception, "Desktop shutdown cleanup failed");
        }

        desktop.ShutdownRequested -= OnShutdownRequested;
        _shutdownCompleted = true;
        desktop.Shutdown();
    }

    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Exchange(ref _disposing, 1);
        GC.SuppressFinalize(this);
        lock (_cleanupGate)
        {
            return new(_cleanupTask ??= CleanupServicesAsync());
        }
    }

    private async Task CleanupServicesAsync()
    {
        Interlocked.Exchange(ref _activationListener, value: null)?.Dispose();
        var services = Services;
        if (services is null)
        {
            await _startupLifetime.StopAsync().ConfigureAwait(true);
            return;
        }
        var cleanupError = await CleanupAsync(
            async () =>
            {
                var startupStop = _startupLifetime.StopAsync();
                var runtimeStop = StopRuntimeAsync(services);
                try { await Task.WhenAll(startupStop, runtimeStop).ConfigureAwait(true); }
                finally { Interlocked.Exchange(ref _activationListener, value: null)?.Dispose(); }
            },
            () => services.GetService<ProfileLoadedMacroSessionPersistenceService>()?.FlushAsync(CancellationToken.None) ?? Task.CompletedTask,
            async () =>
            {
                if (services is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(true);
                }
                else if (services is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }).ConfigureAwait(true);

        if (cleanupError is not null)
        {
            throw cleanupError;
        }
    }

    private static async Task StopRuntimeAsync(IServiceProvider services)
    {
        if (services.GetService<DesktopStartupRuntimeService>() is { } runtime)
        {
            await runtime.StopAsync().ConfigureAwait(true);
        }
    }

    internal static async Task<AggregateException?> CleanupAsync(
        Func<Task> stopRuntime,
        Func<Task> flushProfileState,
        Func<Task> disposeProvider)
    {
        ArgumentNullException.ThrowIfNull(stopRuntime);
        ArgumentNullException.ThrowIfNull(flushProfileState);
        ArgumentNullException.ThrowIfNull(disposeProvider);

        var errors = new List<Exception>();
        try
        {
            await stopRuntime().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }

        try
        {
            await flushProfileState().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }

        try
        {
            await disposeProvider().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }

        return errors.Count is 0
            ? null
            : new AggregateException("Desktop shutdown cleanup failed.", errors);
    }

}
