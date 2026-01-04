using System;
using Microsoft.Extensions.DependencyInjection;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.Windows;
using CrossMacro.Platform.Windows.Services;
using CrossMacro.Platform.Windows.Strategies;

namespace CrossMacro.UI.DependencyInjection;

/// <summary>
/// Windows platform service registrar.
/// </summary>
public class WindowsServiceRegistrar : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IKeyboardLayoutService, WindowsKeyboardLayoutService>();
        services.AddSingleton<IMousePositionProvider, WindowsMousePositionProvider>();
        services.AddSingleton<IEnvironmentInfoProvider, WindowsEnvironmentInfoProvider>();
        
        // No extension notifier needed for Windows (GNOME-specific feature)
        #pragma warning disable CS8634 // Intentionally nullable for optional service
        services.AddSingleton<IExtensionStatusNotifier?>(sp => null);
        #pragma warning restore CS8634
        
        services.AddTransient<Func<IInputSimulator>>(sp => () => new WindowsInputSimulator());
        services.AddTransient<Func<IInputCapture>>(sp => () => new WindowsInputCapture());
        
        services.AddSingleton<ICoordinateStrategyFactory, WindowsCoordinateStrategyFactory>();
        services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
    }
}
