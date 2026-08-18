namespace CrossMacro.UI.DependencyInjection;

internal static class GuiPresentationServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<IDesktopLifetimeContext, DesktopLifetimeContext>();
        _ = services.AddSingleton<LocalizationService>();
        _ = services.AddSingleton<ILocalizationService>(sp => sp.GetRequiredService<LocalizationService>());
        _ = services.AddSingleton<EditorActionDisplayFormatter>();
        _ = services.AddSingleton<ITrayIconService, TrayIconService>();
        _ = services.AddSingleton<IDialogService, DialogService>();
        _ = services.AddSingleton<IPortalScreenReadingGuidanceService>(sp => new PortalScreenReadingGuidanceService(
            sp.GetRequiredService<IDialogService>(),
            sp.GetService<IScreenReadingDiagnosticProvider>(),
            sp.GetService<IScreenReadingCapabilityReadiness>(),
            sp.GetService<IPortalScreenCastRestoreStateService>()));
        _ = services.AddSingleton<IExternalUrlOpener, ExternalUrlOpener>();
        _ = services.AddSingleton<IDirectoryOpener, DirectoryOpener>();
        _ = services.AddSingleton<IThemeDirectoryResolver, ThemeDirectoryResolver>();
        _ = services.AddSingleton<IThemeSampleProvisioner, ThemeSampleProvisioner>();
        _ = services.AddSingleton<IExternalThemeSource, ThemeJsonFileSource>();
        _ = services.AddSingleton<IThemeService>(sp => new ThemeService(Avalonia.Application.Current?.Resources, sp.GetRequiredService<IExternalThemeSource>()));
        _ = services.AddSingleton<Func<ISettingsService>>(sp => () => sp.GetRequiredService<ISettingsService>());
        _ = services.AddSingleton<Func<IThemeService>>(sp => () => sp.GetRequiredService<IThemeService>());
        _ = services.AddSingleton<Func<ITrayIconService>>(sp => () => sp.GetRequiredService<ITrayIconService>());
        _ = services.AddSingleton<Func<ITextExpansionService>>(sp => () => sp.GetRequiredService<ITextExpansionService>());
        _ = services.AddSingleton<Func<LocalizationService>>(sp => () => sp.GetRequiredService<LocalizationService>());
        _ = services.AddSingleton<Func<EditorActionDisplayFormatter>>(sp => () => sp.GetRequiredService<EditorActionDisplayFormatter>());
        _ = services.AddSingleton<Func<MainWindow>>(_ => () => new MainWindow());
        _ = services.AddSingleton<Func<MainWindowViewModel>>(sp => () => sp.GetRequiredService<MainWindowViewModel>());
        _ = services.AddSingleton<Func<IFlatpakQuickSetupService?>>(sp => () => sp.GetService<IFlatpakQuickSetupService>());
        _ = services.AddSingleton<Func<IAppImageQuickSetupService?>>(sp => () => sp.GetService<IAppImageQuickSetupService>());
        _ = services.AddSingleton<Func<IDisplaySessionService?>>(sp => () => sp.GetService<IDisplaySessionService>());
        _ = services.AddSingleton<Func<IPermissionChecker?>>(sp => () => sp.GetService<IPermissionChecker>());
        _ = services.AddSingleton<Func<IInputSimulatorPool?>>(sp => () => sp.GetService<IInputSimulatorPool>());
        _ = services.AddSingleton<Func<IMousePositionProvider?>>(sp => () => sp.GetService<IMousePositionProvider>());
        _ = services.AddSingleton<DesktopStartupInitializationService>();
        _ = services.AddSingleton<ProfileLoadedMacroSessionPersistenceService>();
        _ = services.AddSingleton<IProfileRuntimeParticipant>(sp => sp.GetRequiredService<ProfileLoadedMacroSessionPersistenceService>());
        _ = services.AddSingleton<DesktopPermissionGateService>();
        _ = services.AddSingleton<DesktopQuickSetupGateService>();
        _ = services.AddSingleton<IRuntimeLifecycle>(sp => DesktopStartupRuntimeService.CreateLifecycle(() => sp.GetRequiredService<ITextExpansionService>()));
        _ = services.AddSingleton<DesktopStartupRuntimeService>();
        _ = services.AddSingleton<IDesktopStartupCoordinator>(sp => new DesktopStartupCoordinator(sp.GetRequiredService<DesktopStartupInitializationService>(), sp.GetRequiredService<DesktopPermissionGateService>(), sp.GetRequiredService<DesktopQuickSetupGateService>(), sp.GetRequiredService<DesktopStartupRuntimeService>()));
    }
}
