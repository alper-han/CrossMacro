using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Startup;
using CrossMacro.UI.ViewModels;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.DependencyInjection;

/// <summary>
/// Extension methods for configuring CrossMacro services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Main entry point - registers all services for the application.
    /// </summary>
    public static IServiceCollection AddCrossMacroServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar platformServiceRegistrar)
    {
        services.AddCrossMacroGuiRuntimeServices(platformServiceRegistrar);
        services.AddViewModels();
        
        return services;
    }

    /// <summary>
    /// Backward-compatible alias for GUI runtime service registration.
    /// </summary>
    public static IServiceCollection AddCrossMacroRuntimeServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar platformServiceRegistrar)
    {
        return services.AddCrossMacroGuiRuntimeServices(platformServiceRegistrar);
    }

    /// <summary>
    /// Registers runtime services for GUI execution.
    /// </summary>
    public static IServiceCollection AddCrossMacroGuiRuntimeServices(
        this IServiceCollection services,
        IPlatformServiceRegistrar platformServiceRegistrar)
    {
        ArgumentNullException.ThrowIfNull(platformServiceRegistrar);

        services.TryAddSingleton(GuiStartupOptions.Default);
        services.AddCommonServices();
        platformServiceRegistrar.RegisterPlatformServices(services);
        services.AddSharedPostPlatformServices(includeGuiOnlyServices: true, allowAvaloniaClipboardFallback: true);
        
        return services;
    }

    /// <summary>
    /// Registers platform-agnostic core services.
    /// </summary>
    public static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        return services.AddCrossMacroCommonRuntimeServices();
    }

    /// <summary>
    /// Registers services that depend on platform services being registered first.
    /// </summary>
    public static IServiceCollection AddSharedPostPlatformServices(
        this IServiceCollection services,
        bool includeGuiOnlyServices = true,
        bool allowAvaloniaClipboardFallback = true)
    {
        services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => sp.GetService<InputSimulatorPool>());

        RegisterClipboardServices(services, allowAvaloniaClipboardFallback);

        if (includeGuiOnlyServices)
        {
            services.AddSingleton<ITrayIconService, TrayIconService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IUpdateService, GitHubUpdateService>();
            services.AddSingleton<IExternalUrlOpener, ExternalUrlOpener>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<Func<ISettingsService>>(sp => () => sp.GetRequiredService<ISettingsService>());
            services.AddSingleton<Func<IThemeService>>(sp => () => sp.GetRequiredService<IThemeService>());
            services.AddSingleton<Func<ITrayIconService>>(sp => () => sp.GetRequiredService<ITrayIconService>());
            services.AddSingleton<Func<ITextExpansionService>>(sp => () => sp.GetRequiredService<ITextExpansionService>());
            services.AddSingleton<Func<MainWindowViewModel>>(sp => () => sp.GetRequiredService<MainWindowViewModel>());
            services.AddSingleton<Func<IFlatpakQuickSetupService?>>(sp => () => sp.GetService<IFlatpakQuickSetupService>());
            services.AddSingleton<Func<IAppImageQuickSetupService?>>(sp => () => sp.GetService<IAppImageQuickSetupService>());
            services.AddSingleton<Func<IPermissionChecker?>>(sp => () => sp.GetService<IPermissionChecker>());
            services.AddSingleton<Func<InputSimulatorPool?>>(sp => () => sp.GetService<InputSimulatorPool>());
            services.AddSingleton<Func<IMousePositionProvider?>>(sp => () => sp.GetService<IMousePositionProvider>());
            services.AddSingleton<IDesktopStartupCoordinator>(sp =>
                new DesktopStartupCoordinator(
                    sp.GetRequiredService<IDisplaySessionService>(),
                    sp.GetRequiredService<Func<ISettingsService>>(),
                    sp.GetRequiredService<Func<IThemeService>>(),
                    sp.GetRequiredService<Func<ITrayIconService>>(),
                    sp.GetRequiredService<Func<ITextExpansionService>>(),
                    sp.GetRequiredService<Func<MainWindowViewModel>>(),
                    sp.GetRequiredService<Func<IFlatpakQuickSetupService?>>(),
                    sp.GetRequiredService<Func<IAppImageQuickSetupService?>>(),
                    sp.GetRequiredService<Func<IPermissionChecker?>>(),
                    sp.GetRequiredService<Func<InputSimulatorPool?>>(),
                    sp.GetRequiredService<Func<IMousePositionProvider?>>(),
                    sp.GetRequiredService<GuiStartupOptions>()));
        }

        return services;
    }

    private static void RegisterClipboardServices(IServiceCollection services, bool allowAvaloniaClipboardFallback)
    {
        if (allowAvaloniaClipboardFallback)
        {
            services.AddSingleton<AvaloniaClipboardService>();
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<AvaloniaClipboardService>());
            }
            else
            {
                services.AddSingleton<IProcessRunner, ProcessRunner>();
                services.AddSingleton<LinuxShellClipboardService>();
                services.AddSingleton<IClipboardService, CompositeClipboardService>();
            }

            return;
        }

        if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            services.AddSingleton<LinuxShellClipboardService>();
            services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<LinuxShellClipboardService>());
            return;
        }

        services.AddSingleton<IClipboardService, NoOpClipboardService>();
    }

    /// <summary>
    /// Registers all ViewModels.
    /// </summary>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<RecordingViewModel>();
        services.AddSingleton<PlaybackViewModel>();
        services.AddSingleton<FilesViewModel>();
        services.AddSingleton<TextExpansionViewModel>();
        services.AddSingleton<ScheduleViewModel>();
        services.AddSingleton<ShortcutViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        
        return services;
    }

}
