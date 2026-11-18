using System.Runtime.Versioning;
using Avalonia;
using CrossMacro.Platform.MacOS.DependencyInjection;
using CrossMacro.UI.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI.MacOS;

[SupportedOSPlatform("macos")]
internal sealed class MacOSDesktopPlatform : IDesktopPlatform
{
    private readonly MacOSPlatformServiceRegistrar _registrar = new();

    public void RegisterPlatformServices(IServiceCollection services) => _registrar.RegisterPlatformServices(services);

    public AppBuilder ConfigureAppBuilder(AppBuilder builder) => builder.UseAvaloniaNative().UseSkia();
}
