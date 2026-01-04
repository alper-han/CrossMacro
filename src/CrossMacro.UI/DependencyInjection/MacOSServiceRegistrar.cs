using System;
using Microsoft.Extensions.DependencyInjection;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.MacOS;
using CrossMacro.Platform.MacOS.Services;
using CrossMacro.Platform.MacOS.Strategies;

using System.Runtime.Versioning;

namespace CrossMacro.UI.DependencyInjection;

/// <summary>
/// macOS platform service registrar.
/// </summary>
[SupportedOSPlatform("macos")]
public class MacOSServiceRegistrar : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddMacOSServices();  // Existing extension method
        services.AddSingleton<IKeyboardLayoutService, MacKeyboardLayoutService>();
        services.AddSingleton<IEnvironmentInfoProvider, MacOSEnvironmentInfoProvider>();
        // No extension notifier needed for macOS (GNOME-specific feature)
        #pragma warning disable CS8634 // Intentionally nullable for optional service
        services.AddSingleton<IExtensionStatusNotifier?>(sp => null);
        #pragma warning restore CS8634

        services.AddTransient<Func<IInputSimulator>>(sp => () => new MacOSInputSimulator());
        services.AddTransient<Func<IInputCapture>>(sp => () => new MacOSInputCapture());
        
        services.AddSingleton<ICoordinateStrategyFactory, MacOSCoordinateStrategyFactory>();
        services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
    }
}
