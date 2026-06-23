using System;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Windows.Services;
using CrossMacro.Platform.Windows.Services.ScreenReading;
using CrossMacro.Platform.Windows.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Windows.DependencyInjection;

public sealed class WindowsPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public PlatformClipboardRegistration ClipboardRegistration => PlatformClipboardRegistration.Windows;

    public void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IKeyboardLayoutService, WindowsKeyboardLayoutService>();
        services.AddSingleton<IMousePositionProvider, WindowsMousePositionProvider>();
        services.AddSingleton<IScreenFrameProvider, WindowsScreenFrameProvider>();
        services.AddSingleton<IEnvironmentInfoProvider, WindowsEnvironmentInfoProvider>();
        services.AddSingleton<IPlaybackBehaviorPolicy>(
            _ => new PlaybackBehaviorPolicy(useHybridAbsoluteDragMovement: false));

#pragma warning disable CS8634 // Intentionally nullable for optional service
        services.AddSingleton<IExtensionStatusNotifier?>(sp => null);
#pragma warning restore CS8634

        services.AddTransient<Func<IInputSimulator>>(sp => () => new WindowsInputSimulator());
        services.AddTransient<Func<IInputCapture>>(sp => () => new WindowsInputCapture());

        services.AddSingleton<ICoordinateStrategyFactory, WindowsCoordinateStrategyFactory>();
        services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
    }
}
