using System.Linq;

namespace CrossMacro.UI.ViewModels;

public sealed class DesignShortcutViewModel : ShortcutViewModel
{
    public DesignShortcutViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignShortcutViewModel(DesignPreviewContext context)
        : base(context.ShortcutService, context.DialogService, context.HotkeyService, context.LocalizationService)
    {
        SelectedTask = Tasks.FirstOrDefault();
    }
}
