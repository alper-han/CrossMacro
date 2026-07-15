
namespace CrossMacro.Platform.MacOS.DependencyInjection;

[SupportedOSPlatform("macos")]
public sealed class MacOSPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddSingleton<IKeyboardLayoutService, MacKeyboardLayoutService>();
        services.AddSingleton<IEnvironmentInfoProvider, MacOSEnvironmentInfoProvider>();
        services.AddSingleton<IPlaybackBehaviorPolicy>(
            _ => new MacOSPlaybackBehaviorPolicy());
        services.AddSingleton<IMousePositionProvider, MacOSMousePositionProvider>();
        services.AddSingleton<IScreenFrameProvider, MacOSScreenFrameProvider>();
        services.AddSingleton<IMacOSScreenRecordingPermissionProbe, CoreGraphicsScreenRecordingPermissionProbe>();
        services.AddSingleton<IPermissionChecker, MacOSPermissionCheckerService>();

#pragma warning disable CS8634 // Intentionally nullable for optional service
        services.AddSingleton<IExtensionStatusNotifier?>(sp => null);
#pragma warning restore CS8634

        services.AddTransient<Func<IInputSimulator>>(sp =>
        {
            var permissionChecker = sp.GetRequiredService<IPermissionChecker>();
            return () => new MacOSInputSimulator(MacOSPermissionRequestDelegates.RequestPostEventAccess(permissionChecker));
        });
        services.AddTransient<Func<IInputCapture>>(sp =>
        {
            var permissionChecker = sp.GetRequiredService<IPermissionChecker>();
            return () => new MacOSInputCapture(MacOSPermissionRequestDelegates.RequestListenEventAccess(permissionChecker));
        });

        services.AddSingleton<ICoordinateStrategyFactory, MacOSCoordinateStrategyFactory>();
        services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
    }
}
