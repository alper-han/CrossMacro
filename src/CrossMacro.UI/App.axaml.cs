
namespace CrossMacro.UI;

public class App : Avalonia.Application
{
    private readonly GuiBootstrapContext? _bootstrapContext;
    private bool _shutdownStarted;
    private bool _shutdownCompleted;

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
        ConfigureServices();
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
        if (Volatile.Read(ref _shutdownStarted))
        {
            return;
        }

        try
        {
            await startupCoordinator.StartAsync(desktop).ConfigureAwait(false);
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

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        e.Cancel = true;
        if (Volatile.Read(ref _shutdownStarted))
        {
            return;
        }

        Volatile.Write(ref _shutdownStarted, value: true);
        _ = CompleteShutdownAsync((IClassicDesktopStyleApplicationLifetime)sender!);
    }

    private async Task CompleteShutdownAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var services = Services;
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            errors.Add(ex);
        }

        try
        {
            disposeViewModel();
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
