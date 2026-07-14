using System;
using CrossMacro.Core.Services;
using CrossMacro.Packaging.Abstractions;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Linux.DependencyInjection;

/// <summary>
/// Linux platform service registrar.
/// Handles Wayland/X11/legacy fallback service selection.
/// </summary>
public sealed class LinuxPlatformServiceRegistrar : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        RegisterPlatformServices(services, LinuxEnvironmentVariables.CaptureCurrentSnapshot());
    }

    public void RegisterPlatformServices(IServiceCollection services, LinuxEnvironmentSnapshot environment)
    {
        services.AddSingleton(typeof(LinuxEnvironmentSnapshot), environment);
        services.AddLinuxCoreServices(environment);
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
