
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
