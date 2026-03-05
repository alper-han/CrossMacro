using System;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Cli.DependencyInjection;

public static class CliRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddCrossMacroCliRuntimeServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar platformServiceRegistrar,
        CliRuntimeProfile runtimeProfile = CliRuntimeProfile.OneShot)
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);

        services.AddCommonServices();
        platformServiceRegistrar.RegisterPlatformServices(services);
        services.AddCliPostPlatformServices(runtimeProfile);

        return services;
    }

    private static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        return services.AddCrossMacroCommonRuntimeServices();
    }

    private static IServiceCollection AddCliPostPlatformServices(
        this IServiceCollection services,
        CliRuntimeProfile runtimeProfile)
    {
        services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => runtimeProfile == CliRuntimeProfile.Persistent
                ? sp.GetService<InputSimulatorPool>()
                : null);

        RegisterCliClipboardServices(services);

        return services;
    }

    private static void RegisterCliClipboardServices(IServiceCollection services)
    {
        if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<LinuxShellClipboardService>();
            services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<LinuxShellClipboardService>());
            return;
        }

        services.AddSingleton<IClipboardService, CliNoOpClipboardService>();
    }

    private sealed class CliNoOpClipboardService : IClipboardService
    {
        public bool IsSupported => false;

        public Task SetTextAsync(string text)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync()
        {
            return Task.FromResult<string?>(null);
        }
    }
}
