
namespace CrossMacro.UI.ViewModels;

public sealed class DesignPlaybackViewModel : PlaybackViewModel
{
    public DesignPlaybackViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignPlaybackViewModel(DesignPreviewContext context)
        : base(context.MacroPlayer, context.SettingsService, context.LoadedMacroSession, context.LocalizationService)
    {
        context.LoadedMacroSession.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var first = context.LoadedMacroSession.AddMacro(DesignPreviewSamples.CreateMacro("Refresh Dashboard Loop"));
        first.SequenceRepeatCount = 4;

        var second = context.LoadedMacroSession.AddMacro(DesignPreviewSamples.CreateMacro("Retry Failed Uploads"));
        second.SequenceRepeatCount = 2;
    }
}
