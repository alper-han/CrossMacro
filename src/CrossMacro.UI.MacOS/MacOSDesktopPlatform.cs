using CrossMacro.UI.Hosting;
namespace CrossMacro.UI.MacOS;

[SupportedOSPlatform("macos")]
internal sealed class MacOSDesktopPlatform : IDesktopPlatform
{
    private readonly MacOSPlatformServiceRegistrar _registrar = new();

    public void RegisterPlatformServices(IServiceCollection services) => _registrar.RegisterPlatformServices(services);

    public AppBuilder ConfigureAppBuilder(AppBuilder builder) => builder.UseAvaloniaNative().UseSkia();
}
