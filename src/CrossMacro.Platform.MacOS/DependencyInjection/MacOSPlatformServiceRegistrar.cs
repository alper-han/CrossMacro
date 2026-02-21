using System;
using System.Runtime.Versioning;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.MacOS.Services;
using CrossMacro.Platform.MacOS.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.MacOS.DependencyInjection;

[SupportedOSPlatform("macos")]
public sealed class MacOSPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IKeyboardLayoutService, MacKeyboardLayoutService>();
        services.AddSingleton<IEnvironmentInfoProvider, MacOSEnvironmentInfoProvider>();
        services.AddSingleton<IMousePositionProvider, MacOSMousePositionProvider>();
        services.AddSingleton<IPermissionChecker, MacOSPermissionCheckerService>();

#pragma warning disable CS8634 // Intentionally nullable for optional service
        services.AddSingleton<IExtensionStatusNotifier?>(sp => null);
#pragma warning restore CS8634

        services.AddTransient<Func<IInputSimulator>>(sp => () => new MacOSInputSimulator());
        services.AddTransient<Func<IInputCapture>>(sp => () => new MacOSInputCapture());

        services.AddSingleton<ICoordinateStrategyFactory, MacOSCoordinateStrategyFactory>();
        services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
    }
}
