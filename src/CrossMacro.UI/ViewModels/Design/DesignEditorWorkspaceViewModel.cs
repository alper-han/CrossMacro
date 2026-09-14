namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignEditorWorkspaceViewModel : EditorWorkspaceViewModel
{
    public DesignEditorWorkspaceViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignEditorWorkspaceViewModel(DesignPreviewContext context)
        : this(context, new DesignEditorViewModel(context))
    {
    }

    private DesignEditorWorkspaceViewModel(DesignPreviewContext context, DesignEditorViewModel document)
        : base(context.CreateEditorDocumentFactory(), context.DialogService, context.LocalizationService, uiDispatcher: DesignUiDispatcher.Instance, initialDocument: document)
    {
        NewTab();
    }
}
