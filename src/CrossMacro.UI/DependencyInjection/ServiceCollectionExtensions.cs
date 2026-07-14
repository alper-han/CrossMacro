using System;
using CrossMacro.Application.Automation;
using CrossMacro.Application.Profiles;
using CrossMacro.Application.Runtime;
using CrossMacro.Core.Services;
using CrossMacro.Packaging.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.UI.Startup;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using CrossMacro.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CrossMacro.Platform.Abstractions;

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
        this IServiceCollection services)
    {
        services.AddCrossMacroGuiRuntimeServices();
        services.AddViewModels();

        return services;
    }

    /// <summary>
    /// Registers runtime services for GUI execution.
    /// </summary>
    public static IServiceCollection AddCrossMacroGuiRuntimeServices(
        this IServiceCollection services)
    {
        services.TryAddSingleton(GuiStartupOptions.Default);
        services.AddGuiOnlyServices();

        services.AddSingleton<IManageProfile, ManageProfile>();
        services.AddSingleton<IManageTextExpansion, ManageTextExpansion>();
        services.AddSingleton<IManageSchedule, ManageSchedule>();
        services.AddSingleton<IManageShortcut, ManageShortcut>();
        services.AddSingleton<IManageTrigger, ManageTrigger>();

        return services;
    }

    private static IServiceCollection AddGuiOnlyServices(this IServiceCollection services)
    {
        services.AddSingleton<IDesktopLifetimeContext, DesktopLifetimeContext>();
            services.AddSingleton<LocalizationService>();
            services.AddSingleton<ILocalizationService>(sp => sp.GetRequiredService<LocalizationService>());
            services.AddSingleton<EditorActionDisplayFormatter>();
            services.AddSingleton<ITrayIconService, TrayIconService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IPortalScreenReadingGuidanceService>(sp =>
                new PortalScreenReadingGuidanceService(
                    sp.GetRequiredService<IDialogService>(),
                    sp.GetRequiredService<ISettingsService>(),
                    sp.GetService<IScreenReadingDiagnosticProvider>()));
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
            services.AddSingleton<Func<IInputSimulatorPool?>>(sp => () => sp.GetService<IInputSimulatorPool>());
            services.AddSingleton<Func<IMousePositionProvider?>>(sp => () => sp.GetService<IMousePositionProvider>());
            services.AddSingleton<DesktopStartupInitializationService>();
            services.AddSingleton<DesktopPermissionGateService>();
            services.AddSingleton<DesktopQuickSetupGateService>();
            services.AddSingleton<InputSimulatorWarmupService>();
            services.AddSingleton<IRuntimeLifecycle>(sp =>
                DesktopStartupRuntimeService.CreateLifecycle(
                    () => sp.GetRequiredService<ITextExpansionService>()));
            services.AddSingleton<DesktopStartupRuntimeService>();
            services.AddSingleton<IDesktopStartupCoordinator>(sp =>
                new DesktopStartupCoordinator(
                    sp.GetRequiredService<DesktopStartupInitializationService>(),
                    sp.GetRequiredService<DesktopPermissionGateService>(),
                    sp.GetRequiredService<DesktopQuickSetupGateService>(),
                    sp.GetRequiredService<DesktopStartupRuntimeService>()));
        return services;
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
        services.AddSingleton<TextExpansionViewModel>(sp =>
            new TextExpansionViewModel(
                sp.GetRequiredService<IManageTextExpansion>(),
                sp.GetRequiredService<IDialogService>(),
                sp.GetRequiredService<IEnvironmentInfoProvider>(),
                sp.GetRequiredService<ILocalizationService>()));
        services.AddSingleton<ScheduleViewModel>();
        services.AddSingleton<ShortcutViewModel>();
        services.AddSingleton<TriggerViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        
        return services;
    }

}
