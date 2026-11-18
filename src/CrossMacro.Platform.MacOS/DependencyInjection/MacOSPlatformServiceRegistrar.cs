
namespace CrossMacro.Platform.MacOS.DependencyInjection;

[SupportedOSPlatform("macos")]
public sealed class MacOSPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public static void RegisterNativeClipboardServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddSingleton<MacOSNativeClipboardService>();
        _ = services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<MacOSNativeClipboardService>());
        _ = services.AddSingleton<IImageClipboardService>(sp => sp.GetRequiredService<MacOSNativeClipboardService>());
        _ = services.AddSingleton<IImageClipboardReader>(sp => sp.GetRequiredService<MacOSNativeClipboardService>());
    }

    public void RegisterPlatformServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddSingleton<IKeyboardLayoutService, MacKeyboardLayoutService>();
        _ = services.AddSingleton<IEnvironmentInfoProvider, MacOSEnvironmentInfoProvider>();
        _ = services.AddSingleton<IMousePositionProvider, MacOSMousePositionProvider>();
        _ = services.AddSingleton<IMacOSScreenCaptureBackend, CoreGraphicsMacOSScreenCaptureBackend>();
        _ = services.AddSingleton<IScreenFrameProvider, MacOSScreenFrameProvider>();
        _ = services.AddSingleton<IMacOSPermissionNative, MacOSPermissionNative>();
        _ = services.AddSingleton<MacOSPermissionCheckerService>();
        _ = services.AddSingleton<IPermissionChecker>(sp => sp.GetRequiredService<MacOSPermissionCheckerService>());
        _ = services.AddSingleton<IMacOSPermissionChecker>(sp => sp.GetRequiredService<MacOSPermissionCheckerService>());
        _ = services.AddSingleton<IMacOSScreenRecordingPermissionProbe>(sp => sp.GetRequiredService<MacOSPermissionCheckerService>());
        _ = services.AddSingleton<IWindowManager, MacOSWindowManager>();

        _ = services.AddTransient<Func<IInputSimulator>>(sp =>
        {
            var permissions = sp.GetRequiredService<IMacOSPermissionChecker>();
            return () => new MacOSInputSimulator(permissions.RequestPostEventAccess);
        });
        _ = services.AddTransient<Func<IInputCapture>>(sp =>
        {
            var permissions = sp.GetRequiredService<IMacOSPermissionChecker>();
            return () => new MacOSInputCapture(permissions.RequestListenEventAccess);
        });

        _ = services.AddSingleton<ICoordinateStrategyFactory>(sp =>
            new MacOSCoordinateStrategyFactory(sp.GetRequiredService<IMousePositionProvider>()));
        _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
    }
}
