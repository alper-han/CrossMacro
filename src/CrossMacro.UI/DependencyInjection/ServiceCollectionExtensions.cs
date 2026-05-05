using System;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Packaging.Abstractions;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Startup;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using CrossMacro.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddSharedPostPlatformServices(
            clipboardMode: platformServiceRegistrar.ClipboardRegistration.GuiMode,
            includeGuiOnlyServices: true);
        
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
        GuiClipboardRegistrationMode clipboardMode,
        bool includeGuiOnlyServices = true,
        bool allowAvaloniaClipboardFallback = true)
    {
        services.AddCrossMacroSharedPostPlatformRuntimeServices(
            sp => sp.GetService<InputSimulatorPool>());

        RegisterClipboardServices(services, clipboardMode, allowAvaloniaClipboardFallback);

        if (includeGuiOnlyServices)
        {
            services.AddSingleton<IDesktopLifetimeContext, DesktopLifetimeContext>();
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<ILocalizationService>(sp => sp.GetRequiredService<LocalizationService>());
            services.AddSingleton<EditorActionDisplayFormatter>();
            services.AddSingleton<ITrayIconService, TrayIconService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IUpdateService, GitHubUpdateService>();
            services.AddSingleton<IExternalUrlOpener, ExternalUrlOpener>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<Func<ISettingsService>>(sp => () => sp.GetRequiredService<ISettingsService>());
            services.AddSingleton<Func<IThemeService>>(sp => () => sp.GetRequiredService<IThemeService>());
            services.AddSingleton<Func<ITrayIconService>>(sp => () => sp.GetRequiredService<ITrayIconService>());
            services.AddSingleton<Func<ITextExpansionService>>(sp => () => sp.GetRequiredService<ITextExpansionService>());
            services.AddSingleton<Func<LocalizationService>>(sp => () => sp.GetRequiredService<LocalizationService>());
            services.AddSingleton<Func<EditorActionDisplayFormatter>>(sp => () => sp.GetRequiredService<EditorActionDisplayFormatter>());
            services.AddSingleton<Func<MainWindow>>(_ => () => new MainWindow());
            services.AddSingleton<Func<MainWindowViewModel>>(sp => () => sp.GetRequiredService<MainWindowViewModel>());
            services.AddSingleton<Func<IFlatpakQuickSetupService?>>(sp => () => sp.GetService<IFlatpakQuickSetupService>());
            services.AddSingleton<Func<IAppImageQuickSetupService?>>(sp => () => sp.GetService<IAppImageQuickSetupService>());
            services.AddSingleton<Func<IPermissionChecker?>>(sp => () => sp.GetService<IPermissionChecker>());
            services.AddSingleton<Func<InputSimulatorPool?>>(sp => () => sp.GetService<InputSimulatorPool>());
            services.AddSingleton<Func<IMousePositionProvider?>>(sp => () => sp.GetService<IMousePositionProvider>());
            services.AddSingleton<DesktopStartupInitializationService>();
            services.AddSingleton<DesktopPermissionGateService>();
            services.AddSingleton<DesktopQuickSetupGateService>();
            services.AddSingleton<InputSimulatorWarmupService>();
            services.AddSingleton<DesktopStartupRuntimeService>();
            services.AddSingleton<IDesktopStartupCoordinator>(sp =>
                new DesktopStartupCoordinator(
                    sp.GetRequiredService<DesktopStartupInitializationService>(),
                    sp.GetRequiredService<DesktopPermissionGateService>(),
                    sp.GetRequiredService<DesktopQuickSetupGateService>(),
                    sp.GetRequiredService<DesktopStartupRuntimeService>()));
        }

        return services;
    }

    private static void RegisterClipboardServices(
        IServiceCollection services,
        GuiClipboardRegistrationMode clipboardMode,
        bool allowAvaloniaClipboardFallback)
    {
        services.AddSingleton<AvaloniaClipboardService>();

        if (!allowAvaloniaClipboardFallback)
        {
            services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<AvaloniaClipboardService>());
            return;
        }

        switch (clipboardMode)
        {
            case GuiClipboardRegistrationMode.AvaloniaOnly:
                services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<AvaloniaClipboardService>());
                return;
            case GuiClipboardRegistrationMode.LinuxShellWithAvaloniaFallback:
                services.AddSingleton<IProcessRunner, ProcessRunner>();
                services.AddSingleton<FlatpakHostClipboardService>();
                services.AddSingleton<LinuxShellClipboardService>();
                services.AddSingleton<IClipboardService, CompositeClipboardService>();
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(clipboardMode), clipboardMode, null);
        }
    }

    /// <summary>
    /// Registers all ViewModels.
    /// </summary>
    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddSingleton<ILoadedMacroSession, LoadedMacroSession>();
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
