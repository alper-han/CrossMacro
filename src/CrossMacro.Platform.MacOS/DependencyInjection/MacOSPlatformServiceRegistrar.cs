
namespace CrossMacro.Platform.MacOS.DependencyInjection;

[SupportedOSPlatform("macos")]
public sealed class MacOSPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    private static bool WindowTasksOnly => string.Equals(
        Environment.GetEnvironmentVariable("CROSSMACRO_WINDOW_TASKS_ONLY"), "1", StringComparison.Ordinal);

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
        _ = services.AddSingleton<IScreenFrameProvider, MacOSScreenFrameProvider>();
        _ = services.AddSingleton<IMacOSScreenRecordingPermissionProbe, CoreGraphicsScreenRecordingPermissionProbe>();
        _ = services.AddSingleton<IPermissionChecker, MacOSPermissionCheckerService>();
        _ = services.AddSingleton<IWindowManager, MacOSWindowManager>();


        _ = services.AddTransient<Func<IInputSimulator>>(sp =>
        {
            var permissionChecker = sp.GetRequiredService<IPermissionChecker>();
            return () => WindowTasksOnly
                ? throw new InvalidOperationException("Native desktop input is disabled in window-task mode. Use the configured external executor.")
                : new MacOSInputSimulator(MacOSPermissionRequestDelegates.RequestPostEventAccess(permissionChecker));
        });
        _ = services.AddTransient<Func<IInputCapture>>(sp =>
        {
            var permissionChecker = sp.GetRequiredService<IPermissionChecker>();
            return () => WindowTasksOnly
                ? throw new InvalidOperationException("Native input recording is disabled in window-task mode.")
                : new MacOSInputCapture(MacOSPermissionRequestDelegates.RequestListenEventAccess(permissionChecker));
        });

        _ = services.AddSingleton<ICoordinateStrategyFactory>(sp =>
            new MacOSCoordinateStrategyFactory(sp.GetRequiredService<IMousePositionProvider>()));
        _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
    }
}
