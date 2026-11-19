using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Abstractions.Runtime;

/// <summary>
/// The single per-platform composition entry point shared by the GUI and CLI runtimes.
/// </summary>
/// <remarks>
/// Registers the platform's input, display, window, permission, and clipboard services.
/// A platform that needs a specific <see cref="IRuntimeContext"/> registers it here;
/// the shared composition supplies the default context for platforms that do not.
/// </remarks>
public interface IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services);
}
