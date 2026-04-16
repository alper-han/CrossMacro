using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CrossMacro.Core.Services;
using CrossMacro.UI.DependencyInjection;
using CrossMacro.UI.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI;

public partial class App : Application
{
    internal static IPlatformServiceRegistrar? PlatformServiceRegistrar { get; set; }
    internal static GuiStartupOptions StartupOptions { get; set; } = GuiStartupOptions.Default;
    private IServiceProvider? _serviceProvider;

    public IServiceProvider? Services => _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        if (PlatformServiceRegistrar == null)
        {
            // Allow tooling/design-time hosts to construct App without a platform host project.
            _serviceProvider = new ServiceCollection().BuildServiceProvider();
            return;
        }

        var services = new ServiceCollection();
        services.AddSingleton(StartupOptions);
        services.AddCrossMacroServices(PlatformServiceRegistrar);
        _serviceProvider = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            if (!Design.IsDesignMode && PlatformServiceRegistrar == null)
            {
                throw new InvalidOperationException(
                    "Platform service registrar is not configured. Start the app via a platform host project.");
            }


            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider is not initialized");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

}
