
namespace CrossMacro.Platform.MacOS.DependencyInjection;

[SupportedOSPlatform("macos")]
public sealed class MacOSPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        _ = services.AddSingleton<IKeyboardLayoutService, MacKeyboardLayoutService>();
        _ = services.AddSingleton<IEnvironmentInfoProvider, MacOSEnvironmentInfoProvider>();
        _ = services.AddSingleton<IMousePositionProvider, MacOSMousePositionProvider>();
        _ = services.AddSingleton<IScreenFrameProvider, MacOSScreenFrameProvider>();
        _ = services.AddSingleton<IMacOSScreenRecordingPermissionProbe, CoreGraphicsScreenRecordingPermissionProbe>();
        _ = services.AddSingleton<IPermissionChecker, MacOSPermissionCheckerService>();

#pragma warning disable CS8634 // Intentionally nullable for optional service
        _ = services.AddSingleton<IExtensionStatusNotifier?>(_ => null);
#pragma warning restore CS8634

        _ = services.AddTransient<Func<IInputSimulator>>(sp =>
        {
            var permissionChecker = sp.GetRequiredService<IPermissionChecker>();
            return () => new MacOSInputSimulator(MacOSPermissionRequestDelegates.RequestPostEventAccess(permissionChecker));
        });
        _ = services.AddTransient<Func<IInputCapture>>(sp =>
        {
            var permissionChecker = sp.GetRequiredService<IPermissionChecker>();
            return () => new MacOSInputCapture(MacOSPermissionRequestDelegates.RequestListenEventAccess(permissionChecker));
        });

        _ = services.AddSingleton<ICoordinateStrategyFactory>(sp =>
            new MacOSCoordinateStrategyFactory(sp.GetRequiredService<IMousePositionProvider>()));
        _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
    }
}
