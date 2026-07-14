using System;
using System.Runtime.Versioning;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Windows.Services;
using CrossMacro.Platform.Windows.Services.ScreenReading;
using CrossMacro.Platform.Windows.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Windows.DependencyInjection;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public static void RegisterCliClipboardServices(IServiceCollection services)
    {
        services.AddSingleton(sp => new StaMessageThread("CrossMacro_WindowsCliClipboard"));
        services.AddSingleton<IClipboardService, WindowsCliClipboardService>();
        services.AddSingleton<IImageClipboardService, WindowsCliImageClipboardService>();
    }

    public static void RegisterGuiImageClipboardServices(IServiceCollection services)
    {
        services.AddSingleton(sp => new StaMessageThread("CrossMacro_WindowsGuiClipboard"));
        services.AddSingleton<IImageClipboardService, WindowsCliImageClipboardService>();
    }

    public void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IKeyboardLayoutService, WindowsKeyboardLayoutService>();
        services.AddSingleton<IMousePositionProvider, WindowsMousePositionProvider>();
        services.AddSingleton<IScreenFrameProvider, WindowsScreenFrameProvider>();
        services.AddSingleton<IEnvironmentInfoProvider, WindowsEnvironmentInfoProvider>();
        services.AddSingleton<IWindowManager, WindowsWindowManager>();
        services.AddSingleton<IPlaybackBehaviorPolicy>(
            _ => new WindowsPlaybackBehaviorPolicy());

#pragma warning disable CS8634 // Intentionally nullable for optional service
        services.AddSingleton<IExtensionStatusNotifier?>(sp => null);
#pragma warning restore CS8634

        services.AddTransient<Func<IInputSimulator>>(sp => () => new WindowsInputSimulator());
        services.AddTransient<Func<IInputCapture>>(sp => () => new WindowsInputCapture());

        services.AddSingleton<ICoordinateStrategyFactory, WindowsCoordinateStrategyFactory>();
        services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();

    }
}
