
namespace CrossMacro.UI.ViewModels;

public sealed class DesignFilesViewModel : FilesViewModel
{
    public DesignFilesViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignFilesViewModel(DesignPreviewContext context)
        : base(context.MacroFileManager, context.DialogService, context.LoadedMacroSession, context.LocalizationService)
    {
        context.LoadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var first = context.LoadedMacroSession.AddMacro(
            DesignPreviewSamples.CreateMacro("Nightly Export Retry"),
            "/tmp/nightly-export-retry.macro");
        first.SequenceRepeatCount = 3;

        var second = context.LoadedMacroSession.AddMacro(
            DesignPreviewSamples.CreateMacro("Refresh Dashboard Loop"),
            "/tmp/refresh-dashboard-loop.macro");
        second.SequenceRepeatCount = 2;
    }
}
