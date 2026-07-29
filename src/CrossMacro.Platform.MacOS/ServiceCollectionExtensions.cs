

namespace CrossMacro.Platform.MacOS;

public static class ServiceCollectionExtensions
{
    [SupportedOSPlatform("macos")]
    public static IServiceCollection AddMacOSServices(this IServiceCollection services)
    {
        _ = services.AddTransient<IInputCapture>(sp =>
        {
            var permissionChecker = sp.GetRequiredService<IPermissionChecker>();
            return new MacOSInputCapture(MacOSPermissionRequestDelegates.RequestListenEventAccess(permissionChecker));
        });
        _ = services.AddTransient<IInputSimulator>(sp =>
        {
            var permissionChecker = sp.GetRequiredService<IPermissionChecker>();
            return new MacOSInputSimulator(MacOSPermissionRequestDelegates.RequestPostEventAccess(permissionChecker));
        });
        _ = services.AddSingleton<IMousePositionProvider, MacOSMousePositionProvider>();
        _ = services.AddSingleton<IPermissionChecker, MacOSPermissionCheckerService>();
        return services;
    }
}
