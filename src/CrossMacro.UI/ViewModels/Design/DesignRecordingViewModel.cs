
namespace CrossMacro.UI.ViewModels;

public sealed class DesignRecordingViewModel : RecordingViewModel
{
    public DesignRecordingViewModel() : this(new DesignPreviewContext())
    {
    }

    internal DesignRecordingViewModel(DesignPreviewContext context)
        : base(context.MacroRecorder, context.HotkeyService, context.SettingsService, context.LocalizationService, context.RuntimeContext)
    {
        SetMacro(DesignPreviewSamples.CreateMacro("Invoice Form Fill"));
    }
}
