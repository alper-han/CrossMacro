namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignEditorWorkspaceViewModel : EditorWorkspaceViewModel
{
    public DesignEditorWorkspaceViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    private DesignEditorWorkspaceViewModel(DesignPreviewContext context)
        : base(new DesignEditorViewModel(context), context.DialogService, context.LocalizationService)
    {
        NewTab();
    }
}
