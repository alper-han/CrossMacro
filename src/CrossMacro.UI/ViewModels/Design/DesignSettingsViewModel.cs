namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignSettingsViewModel : SettingsViewModel
{
    public DesignSettingsViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignSettingsViewModel(DesignPreviewContext context)
        : base(
            context.HotkeyService,
            context.SettingsService,
            context.TextExpansionService,
            context.HotkeySettings,
            context.ExternalUrlOpener,
            context.RuntimeLogLevelService,
            context.ThemeService,
            context.RuntimeContext,
            context.LocalizationService)
    { /* Empty */ }
}
