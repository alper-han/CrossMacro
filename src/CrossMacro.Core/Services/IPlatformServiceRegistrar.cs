using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Core.Services;

/// <summary>
/// Registers platform-specific service implementations into the DI container.
/// </summary>
public interface IPlatformServiceRegistrar
{
    void RegisterPlatformServices(IServiceCollection services);
}
