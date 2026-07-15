
namespace CrossMacro.UI;

public partial class App : Avalonia.Application
{
    private readonly GuiBootstrapContext? _bootstrapContext;
    private IServiceProvider? _serviceProvider;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;

    public App()
    {
    }

    internal App(GuiBootstrapContext bootstrapContext)
    {
        _bootstrapContext = bootstrapContext ?? throw new ArgumentNullException(nameof(bootstrapContext));
    }

    public IServiceProvider? Services => _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        if (_bootstrapContext is null)
        {
            // Allow tooling/design-time hosts to construct App without a platform host project.
            _serviceProvider = new ServiceCollection().BuildServiceProvider();
            return;
        }

        var services = new ServiceCollection();
        services.AddSingleton(_bootstrapContext.StartupOptions);
        _bootstrapContext.ConfigureServices(services);
        _bootstrapContext.ConfigureRuntimeServices(services);
        services.AddCrossMacroServices();
        _serviceProvider = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            if (!Design.IsDesignMode && _bootstrapContext is null)
            {
                throw new InvalidOperationException(
                    "Platform service composition is not configured. Start the app via a platform host project.");
            }


            if (_serviceProvider is null)
            {
                throw new InvalidOperationException("Service provider is not initialized");
            }

            var desktopLifetime = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime;
            AttachDesktopLifetime(desktopLifetime);
            desktopLifetime.ShutdownRequested += OnShutdownRequested;
            QueueDesktopStartup(desktopLifetime);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void AttachDesktopLifetime(IClassicDesktopStyleApplicationLifetime desktopLifetime)
    {
        var context = _serviceProvider?.GetService<IDesktopLifetimeContext>();
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
        catch (Exception ex)
        {
            SerilogLog.Error(ex, "Desktop startup initialization failed");
            Dispatcher.UIThread.Post(() => desktop.Shutdown(1), DispatcherPriority.Send);
        }
    }

    private IDesktopStartupCoordinator GetDesktopStartupCoordinator()
    {
        var services = _serviceProvider
            ?? throw new InvalidOperationException("Service provider is not initialized.");

        return services.GetRequiredService<IDesktopStartupCoordinator>();
    }

    private static async Task RunStartupAsync(
        IDesktopStartupCoordinator startupCoordinator,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            await startupCoordinator.StartAsync(desktop);
        }
        catch (Exception ex)
        {
            SerilogLog.Error(ex, "Desktop startup failed");
            desktop.Shutdown(1);
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        e.Cancel = true;
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;
        _ = CompleteShutdownAsync((IClassicDesktopStyleApplicationLifetime)sender!);
    }

    private async Task CompleteShutdownAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var services = _serviceProvider;
        if (services is not null)
        {
            var cleanupError = await CleanupAsync(
                () => services.GetService<DesktopStartupRuntimeService>()?.StopAsync() ?? Task.CompletedTask,
                () => services.GetService<MainWindowViewModel>()?.Dispose(),
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
                SerilogLog.Error(cleanupError, "Desktop shutdown cleanup failed");
            }
        }

        desktop.ShutdownRequested -= OnShutdownRequested;
        _shutdownCompleted = true;
        desktop.Shutdown();
    }

    internal static async Task<AggregateException?> CleanupAsync(
        Func<Task> stopRuntime,
        Action disposeViewModel,
        Func<Task> disposeProvider)
    {
        ArgumentNullException.ThrowIfNull(stopRuntime);
        ArgumentNullException.ThrowIfNull(disposeViewModel);
        ArgumentNullException.ThrowIfNull(disposeProvider);

        var errors = new List<Exception>();
        try
        {
            await stopRuntime().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        try
        {
            disposeViewModel();
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        try
        {
            await disposeProvider().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            errors.Add(ex);
        }

        return errors.Count is 0
            ? null
            : new AggregateException("Desktop shutdown cleanup failed.", errors);
    }

}
