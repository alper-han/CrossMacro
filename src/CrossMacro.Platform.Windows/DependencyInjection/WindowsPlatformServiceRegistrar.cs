using CrossMacro.Core.Services.Clipboard;
using CrossMacro.Platform.Abstractions.Input.Keyboard;
using CrossMacro.Platform.Abstractions.Input.Pointer;
using CrossMacro.Platform.Abstractions.Input.Simulation;
using CrossMacro.Platform.Abstractions.Recording.Strategies;
using CrossMacro.Platform.Abstractions.Runtime;
using CrossMacro.Platform.Windows.Services;
using CrossMacro.Platform.Windows.Services.Clipboard;
using CrossMacro.Platform.Windows.Services.Input;
using CrossMacro.Platform.Windows.Services.Runtime;
using CrossMacro.Platform.Windows.Services.ScreenReading;
using CrossMacro.Platform.Windows.Services.WindowManagement;
using CrossMacro.Platform.Windows.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Windows.DependencyInjection;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddSingleton<IKeyboardLayoutService, WindowsKeyboardLayoutService>();
        _ = services.AddSingleton<IMousePositionProvider, WindowsMousePositionProvider>();
        _ = services.AddSingleton<IScreenFrameProvider, WindowsScreenFrameProvider>();
        _ = services.AddSingleton<IEnvironmentInfoProvider, WindowsEnvironmentInfoProvider>();
        _ = services.AddSingleton<IWindowManager, WindowsWindowManager>();

        _ = services.AddTransient<Func<IInputSimulator>>(sp => () => new WindowsInputSimulator());
        _ = services.AddTransient<Func<IInputCapture>>(sp => () => new WindowsInputCapture());

        _ = services.AddSingleton<ICoordinateStrategyFactory, WindowsCoordinateStrategyFactory>();
        _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
        RegisterNativeClipboardServices(services);
    }

    private static void RegisterNativeClipboardServices(IServiceCollection services)
    {
        _ = services.AddSingleton(_ => new StaMessageThread("CrossMacro_WindowsNativeClipboard"));
        _ = services.AddSingleton(sp => new Lazy<StaMessageThread>(
            sp.GetRequiredService<StaMessageThread>,
            LazyThreadSafetyMode.ExecutionAndPublication));
        _ = services.AddSingleton<IClipboardService, WindowsNativeClipboardService>();
        _ = services.AddSingleton<WindowsNativeImageClipboardService>();
        _ = services.AddSingleton<IImageClipboardService>(sp => sp.GetRequiredService<WindowsNativeImageClipboardService>());
        _ = services.AddSingleton<IImageClipboardReader>(sp => sp.GetRequiredService<WindowsNativeImageClipboardService>());
    }
}
