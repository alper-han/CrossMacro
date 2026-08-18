namespace CrossMacro.UI.DependencyInjection;

internal static class ViewModelServiceRegistration
{
    internal static void Register(IServiceCollection services)
    {
        _ = services.AddSingleton<ILoadedMacroSession, LoadedMacroSession>();
        _ = services.AddSingleton<RecordingViewModel>();
        _ = services.AddSingleton<PlaybackViewModel>();
        _ = services.AddSingleton<FilesViewModel>();
        _ = services.AddSingleton<TextExpansionViewModel>(sp => new TextExpansionViewModel(sp.GetRequiredService<IManageTextExpansion>(), sp.GetRequiredService<IDialogService>(), sp.GetRequiredService<IEnvironmentInfoProvider>(), sp.GetRequiredService<ILocalizationService>()));
        _ = services.AddSingleton<ScheduleViewModel>();
        _ = services.AddSingleton<ShortcutViewModel>();
        _ = services.AddSingleton<TriggerViewModel>();
        _ = services.AddSingleton<SettingsViewModel>();
        _ = services.AddSingleton<EditorViewModel>();
        _ = services.AddSingleton<MainWindowViewModel>();
    }
}
