using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Services;
using Microsoft.Extensions.DependencyInjection;

using System.Runtime.Versioning;

namespace CrossMacro.Platform.MacOS;

public static class ServiceCollectionExtensions
{
    [SupportedOSPlatform("macos")]
    public static IServiceCollection AddMacOSServices(this IServiceCollection services)
    {
        services.AddTransient<IInputCapture>(sp =>
        {
            var permissionChecker = sp.GetRequiredService<IPermissionChecker>();
            return new MacOSInputCapture(MacOSPermissionRequestDelegates.RequestListenEventAccess(permissionChecker));
        });
        services.AddTransient<IInputSimulator>(sp =>
        {
            var permissionChecker = sp.GetRequiredService<IPermissionChecker>();
            return new MacOSInputSimulator(MacOSPermissionRequestDelegates.RequestPostEventAccess(permissionChecker));
        });
        services.AddSingleton<IMousePositionProvider, MacOSMousePositionProvider>();
        services.AddSingleton<IPermissionChecker, MacOSPermissionCheckerService>();
        return services;
    }
}
