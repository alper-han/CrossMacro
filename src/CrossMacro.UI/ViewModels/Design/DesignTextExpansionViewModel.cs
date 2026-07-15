using System.Collections.ObjectModel;
using CrossMacro.Core.Models;
using CrossMacro.UI.Models;

namespace CrossMacro.UI.ViewModels;

public sealed class DesignTextExpansionViewModel : TextExpansionViewModel
{
    public DesignTextExpansionViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignTextExpansionViewModel(DesignPreviewContext context)
        : base(context.TextExpansionStore, context.DialogService, context.EnvironmentInfoProvider, context.LocalizationService)
    {
        TriggerInput = ":sync-ok";
        ReplacementInput = "Inventory sync completed successfully";
        SelectedInsertionMode = TextInsertionMode.Paste;
        SelectedPasteMethod = PasteMethod.CtrlShiftV;
        Expansions = new ObservableCollection<TextExpansion>(DesignPreviewSamples.CreateTextExpansions());
        OnPropertyChanged(nameof(HasExpansions));
    }
}
