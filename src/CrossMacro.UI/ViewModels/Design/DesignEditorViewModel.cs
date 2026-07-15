using System.Linq;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

public sealed class DesignEditorViewModel : EditorViewModel
{
    public DesignEditorViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignEditorViewModel(DesignPreviewContext context)
        : base(
            context.EditorActionConverter,
            context.EditorActionValidator,
            context.CoordinateCaptureService,
            context.MacroFileManager,
            context.DialogService,
            context.KeyCodeMapper,
            context.MacroPlayer,
            context.LocalizationService,
            new CrossMacro.UI.Localization.EditorActionDisplayFormatter(context.LocalizationService))
    {
        MacroName = "Recover Failed Export";

        foreach (var action in DesignPreviewSamples.CreateEditorActions())
        {
            Actions.Add(action);
        }

        foreach (var warning in DesignPreviewSamples.CreateEditorWarnings())
        {
            LoadWarnings.Add(warning);
        }

        SelectedAction = Actions.FirstOrDefault(action => action.Type is EditorActionType.MouseClick);
        SkipInitialZeroZero = true;
    }
}
