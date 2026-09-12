
namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignFilesViewModel : FilesViewModel
{
    private const string DesignPreviewMacroDirectory = "/home/demo/macros/design-preview";

    public DesignFilesViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignFilesViewModel(DesignPreviewContext context)
        : base(context.MacroFileManager, context.DialogService, context.LoadedMacroSession, context.LocalizationService)
    {
        context.LoadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var first = context.LoadedMacroSession.AddMacro(
            DesignPreviewSamples.CreateMacro("Nightly Export Retry"),
            $"{DesignPreviewMacroDirectory}/nightly-export-retry.macro");
        first.SequenceRepeatCount = 3;

        var second = context.LoadedMacroSession.AddMacro(
            DesignPreviewSamples.CreateMacro("Refresh Dashboard Loop"),
            $"{DesignPreviewMacroDirectory}/refresh-dashboard-loop.macro");
        second.SequenceRepeatCount = 2;
    }
}
