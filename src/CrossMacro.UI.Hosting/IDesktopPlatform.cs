using Avalonia;
using CrossMacro.Platform.Abstractions.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI.Hosting;

/// <summary>
/// What a desktop executable supplies to the shared composition root: one registrar for the
/// platform graph (runtime context and clipboard included) and its Avalonia windowing setup.
/// </summary>
public interface IDesktopPlatform : IPlatformServiceRegistrar
{
    /// <summary>Registers services that exist only in the GUI process, ahead of the shared GUI graph.</summary>
    public void RegisterGuiServices(IServiceCollection services)
    {
        // Platforms without GUI-only services inherit this empty registration.
    }

    public AppBuilder ConfigureAppBuilder(AppBuilder builder);
}
