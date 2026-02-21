using System;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.Windows.Services;
using CrossMacro.Platform.Windows.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Windows.DependencyInjection;

public sealed class WindowsPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IKeyboardLayoutService, WindowsKeyboardLayoutService>();
        services.AddSingleton<IMousePositionProvider, WindowsMousePositionProvider>();
        services.AddSingleton<IEnvironmentInfoProvider, WindowsEnvironmentInfoProvider>();

#pragma warning disable CS8634 // Intentionally nullable for optional service
        services.AddSingleton<IExtensionStatusNotifier?>(sp => null);
#pragma warning restore CS8634

        services.AddTransient<Func<IInputSimulator>>(sp => () => new WindowsInputSimulator());
        services.AddTransient<Func<IInputCapture>>(sp => () => new WindowsInputCapture());

        services.AddSingleton<ICoordinateStrategyFactory, WindowsCoordinateStrategyFactory>();
        services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
    }
}
