using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
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
        services.AddCliPostPlatformServices(runtimeProfile, platformServiceRegistrar.ClipboardRegistration.CliMode);

        return services;
    }

    private static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        return services.AddCrossMacroCommonRuntimeServices();
    }

    private static IServiceCollection AddCliPostPlatformServices(
        this IServiceCollection services,
        CliRuntimeProfile runtimeProfile,
        CliClipboardRegistrationMode clipboardMode)
    {
        services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => runtimeProfile == CliRuntimeProfile.Persistent
                ? sp.GetService<InputSimulatorPool>()
                : null);

        RegisterCliClipboardServices(services, clipboardMode);

        return services;
    }

    private static void RegisterCliClipboardServices(
        IServiceCollection services,
        CliClipboardRegistrationMode clipboardMode)
    {
        switch (clipboardMode)
        {
            case CliClipboardRegistrationMode.LinuxShellOnly:
                services.AddSingleton<IProcessRunner, ProcessRunner>();
                services.AddSingleton<LinuxShellClipboardService>();
                services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<LinuxShellClipboardService>());
                return;
            case CliClipboardRegistrationMode.NoOp:
                services.AddSingleton<IClipboardService, CliNoOpClipboardService>();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(clipboardMode), clipboardMode, null);
        }
    }

    private sealed class CliNoOpClipboardService : IClipboardService
    {
        public bool IsSupported => false;

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
