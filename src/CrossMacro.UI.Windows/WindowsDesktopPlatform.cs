using System.Runtime.Versioning;
using Avalonia;
using CrossMacro.Platform.Windows.DependencyInjection;
using CrossMacro.UI.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.UI.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsDesktopPlatform : IDesktopPlatform
{
    private readonly WindowsPlatformServiceRegistrar _registrar = new();

    public void RegisterPlatformServices(IServiceCollection services) => _registrar.RegisterPlatformServices(services);

    public AppBuilder ConfigureAppBuilder(AppBuilder builder) => builder.UseWin32().UseSkia();
}
