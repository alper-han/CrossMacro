using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CrossMacro.Core.Services;
using CrossMacro.UI.DependencyInjection;
using CrossMacro.UI.Services;
using CrossMacro.UI.Startup;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Threading.Tasks;

namespace CrossMacro.UI;

public partial class App : Application
{
    private readonly GuiBootstrapContext? _bootstrapContext;
    private IServiceProvider? _serviceProvider;

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
        if (_bootstrapContext == null)
        {
            // Allow tooling/design-time hosts to construct App without a platform host project.
            _serviceProvider = new ServiceCollection().BuildServiceProvider();
            return;
        }

        var services = new ServiceCollection();
        services.AddSingleton(_bootstrapContext.StartupOptions);
        services.AddCrossMacroServices(_bootstrapContext.PlatformServiceRegistrar);
        _serviceProvider = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            if (!Design.IsDesignMode && _bootstrapContext == null)
            {
                throw new InvalidOperationException(
                    "Platform service registrar is not configured. Start the app via a platform host project.");
            }


            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider is not initialized");
            }

            var desktopLifetime = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime;
            AttachDesktopLifetime(desktopLifetime);
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
            Log.Error(ex, "Desktop startup initialization failed");
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
            Log.Error(ex, "Desktop startup failed");
            desktop.Shutdown(1);
        }
    }

}
