
namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignTextExpansionViewModel : TextExpansionViewModel
{
    public DesignTextExpansionViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignTextExpansionViewModel(DesignPreviewContext context)
        : base(new ManageTextExpansion(context.TextExpansionStore, context.ProfileManager), context.DialogService, context.EnvironmentInfoProvider, context.LocalizationService, uiDispatcher: DesignUiDispatcher.Instance)
    {
        _ = InitializeAsync();
        TriggerInput = ":sync-ok";
        ReplacementInput = "Inventory sync completed successfully";
        SelectedInsertionMode = TextInsertionMode.Paste;
        SelectedPasteMethod = PasteMethod.CtrlShiftV;
        Expansions = new ObservableCollection<TextExpansionEntry>(DesignPreviewSamples.CreateTextExpansions());
        OnPropertyChanged(nameof(HasExpansions));
    }
}
