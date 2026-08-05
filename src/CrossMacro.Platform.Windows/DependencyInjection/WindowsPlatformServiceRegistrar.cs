
namespace CrossMacro.Platform.Windows.DependencyInjection;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public static void RegisterCliClipboardServices(IServiceCollection services)
    {
        RegisterStaClipboardThread(services, "CrossMacro_WindowsCliClipboard");
        _ = services.AddSingleton<IClipboardService, WindowsCliClipboardService>();
        _ = services.AddSingleton<IImageClipboardService, WindowsCliImageClipboardService>();
    }

    public static void RegisterGuiImageClipboardServices(IServiceCollection services)
    {
        RegisterStaClipboardThread(services, "CrossMacro_WindowsGuiClipboard");
        _ = services.AddSingleton<IImageClipboardService, WindowsCliImageClipboardService>();
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
