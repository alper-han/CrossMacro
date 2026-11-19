using CrossMacro.Core.Services.Runtime;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Infrastructure.Services.Runtime;
using CrossMacro.Infrastructure.Services.Settings;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrossMacro.Infrastructure.DependencyInjection;

internal static class CommonRuntimeServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        services.TryAddSingleton(_ => ApplicationPathsEnvironment.CaptureCurrent());
        _ = services.AddSingleton<IHotkeyConfigurationService>(sp => new HotkeyConfigurationService(sp.GetRequiredService<ApplicationPaths>().ConfigDirectory));
        _ = services.AddSingleton<ISettingsService>(sp => new SettingsService(sp.GetRequiredService<ApplicationPaths>().ConfigDirectory));
        _ = services.AddSingleton<HotkeySettings>(sp => sp.GetRequiredService<IHotkeyConfigurationService>().Load());
        services.TryAddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IShellCommandRunner>(sp => sp.GetRequiredService<IRuntimeContext>().IsFlatpak ? new FlatpakSandboxShellCommandRunner() : new ShellCommandRunner());
    }
}
