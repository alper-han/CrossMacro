
namespace CrossMacro.UI.ViewModels.Design;

public sealed class DesignRecordingViewModel : RecordingViewModel
{
    public DesignRecordingViewModel() : this(new DesignPreviewContext()) { /* Empty */ }

    internal DesignRecordingViewModel(DesignPreviewContext context)
        : base(context.MacroRecorder, context.HotkeyService, context.SettingsService, context.LocalizationService, context.RuntimeContext)
    {
        SetMacro(DesignPreviewSamples.CreateMacro("Invoice Form Fill"));
    }
}
