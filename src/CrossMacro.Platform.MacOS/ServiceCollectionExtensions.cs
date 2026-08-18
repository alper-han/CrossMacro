

namespace CrossMacro.Platform.MacOS;

public static class ServiceCollectionExtensions
{
    [SupportedOSPlatform("macos")]
    public static IServiceCollection AddMacOSServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // MacOSPlatformServiceRegistrar is the canonical composition path used by
        // the macOS hosts. Keep this legacy extension as an explicit compatibility
        // wrapper, then expose the historical direct services without duplicating
        // their construction policy.
        new DependencyInjection.MacOSPlatformServiceRegistrar().RegisterPlatformServices(services);
        _ = services.AddTransient<IInputCapture>(sp => sp.GetRequiredService<Func<IInputCapture>>()());
        _ = services.AddTransient<IInputSimulator>(sp => sp.GetRequiredService<Func<IInputSimulator>>()());
        return services;
    }
}
