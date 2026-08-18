
namespace CrossMacro.Platform.Windows.DependencyInjection;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public static void RegisterCliClipboardServices(IServiceCollection services)
    {
        RegisterNativeClipboardServices(services, "CrossMacro_WindowsNativeClipboard");
    }

    public static void RegisterGuiClipboardServices(IServiceCollection services)
    {
        RegisterNativeClipboardServices(services, "CrossMacro_WindowsGuiClipboard");
    }

    private static void RegisterNativeClipboardServices(IServiceCollection services, string threadName)
    {
        RegisterStaClipboardThread(services, threadName);
        _ = services.AddSingleton<IClipboardService, WindowsNativeClipboardService>();
        _ = services.AddSingleton<IImageClipboardService, WindowsNativeImageClipboardService>();
    }

    private static void RegisterStaClipboardThread(IServiceCollection services, string name)
    {
        _ = services.AddSingleton(_ => new StaMessageThread(name));
        _ = services.AddSingleton(sp => new Lazy<StaMessageThread>(
            sp.GetRequiredService<StaMessageThread>,
            LazyThreadSafetyMode.ExecutionAndPublication));
    }

    public void RegisterPlatformServices(IServiceCollection services)
    {
        _ = services.AddSingleton<IKeyboardLayoutService, WindowsKeyboardLayoutService>();
        _ = services.AddSingleton<IMousePositionProvider, WindowsMousePositionProvider>();
        _ = services.AddSingleton<IScreenFrameProvider, WindowsScreenFrameProvider>();
        _ = services.AddSingleton<IEnvironmentInfoProvider, WindowsEnvironmentInfoProvider>();
        _ = services.AddSingleton<IWindowManager, WindowsWindowManager>();
#pragma warning disable CS8634 // Intentionally nullable for optional service
        _ = services.AddSingleton<IExtensionStatusNotifier?>(_ => null);
#pragma warning restore CS8634

        _ = services.AddTransient<Func<IInputSimulator>>(sp => () => new WindowsInputSimulator());
        _ = services.AddTransient<Func<IInputCapture>>(sp => () => new WindowsInputCapture());

        _ = services.AddSingleton<ICoordinateStrategyFactory, WindowsCoordinateStrategyFactory>();
        _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();

    }
}
