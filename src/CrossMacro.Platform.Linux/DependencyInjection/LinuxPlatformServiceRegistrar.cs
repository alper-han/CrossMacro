using System;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Packaging.Abstractions;
using CrossMacro.Platform.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Linux.DependencyInjection;

/// <summary>
/// Linux platform service registrar.
/// Handles Wayland/X11/legacy fallback service selection.
/// </summary>
public sealed class LinuxPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public PlatformClipboardRegistration ClipboardRegistration => PlatformClipboardRegistration.Linux;

    public void RegisterPlatformServices(IServiceCollection services)
    {
        services.AddLinuxCoreServices();
        services.AddLinuxLegacyImplementations();
        services.AddLinuxIpcImplementations();
        services.AddLinuxX11Implementations();
        services.AddLinuxFactories();
        services.AddLinuxInputFactories();
        services.AddLinuxStrategySelectors();
        services.AddLinuxPositionProviderSelectors();
        services.AddLinuxCoordinateStrategy();
        services.AddLinuxInputSimulatorPool();
    }
}
