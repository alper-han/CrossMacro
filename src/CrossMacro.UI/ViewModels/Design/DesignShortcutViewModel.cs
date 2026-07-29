
namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignShortcutViewModel : ShortcutViewModel
{
    public DesignShortcutViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignShortcutViewModel(DesignPreviewContext context)
        : base(context.ShortcutService, context.DialogService, context.HotkeyService, context.LocalizationService)
    {
        SelectedTask = Tasks.FirstOrDefault();
    }
}
