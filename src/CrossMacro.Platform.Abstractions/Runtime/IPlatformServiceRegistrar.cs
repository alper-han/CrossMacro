
namespace CrossMacro.Platform.Abstractions.Runtime;

/// <summary>
/// Registers platform-specific service implementations into the DI container.
/// </summary>
public interface IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services);
}
