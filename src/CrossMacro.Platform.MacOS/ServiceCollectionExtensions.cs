using CrossMacro.Core.Services;
using CrossMacro.Platform.MacOS.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.MacOS;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMacOSServices(this IServiceCollection services)
    {
        services.AddTransient<IInputCapture, MacOSInputCapture>();
        services.AddTransient<IInputSimulator, MacOSInputSimulator>();
        services.AddSingleton<IMousePositionProvider, MacOSMousePositionProvider>();
        return services;
    }
}
