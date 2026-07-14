using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Registers platform-specific service implementations into the DI container.
/// </summary>
public interface IPlatformServiceRegistrar
{
    void RegisterPlatformServices(IServiceCollection services);
}
