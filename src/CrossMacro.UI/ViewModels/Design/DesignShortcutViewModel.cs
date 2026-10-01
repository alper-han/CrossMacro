
namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignShortcutViewModel : ShortcutViewModel
{
    public DesignShortcutViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignShortcutViewModel(DesignPreviewContext context)
        : base(new ManageShortcut(context.ShortcutService, context.ShortcutService), context.ShortcutService, context.DialogService, context.HotkeyService, context.LocalizationService, context.ShortcutHotkeyNormalizer, uiDispatcher: DesignUiDispatcher.Instance)
    {
        _ = InitializeAsync();
    }
}
