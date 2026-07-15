
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignMacroRecorder : IMacroRecorder
{
    public bool IsRecording { get; private set; }

    public event EventHandler<MacroEvent>? EventRecorded
    {
        add { }
        remove { }
    }

    public Task StartRecordingAsync(bool recordMouse, bool recordKeyboard, IEnumerable<int>? ignoredKeys = null, bool forceRelative = false, bool skipInitialZero = false, CancellationToken cancellationToken = default)
    {
        IsRecording = true;
        return Task.CompletedTask;
    }

    public MacroSequence StopRecording()
    {
        IsRecording = false;
        return DesignPreviewSamples.CreateMacro();
    }

    public MacroSequence? GetCurrentRecording() => DesignPreviewSamples.CreateMacro();

    public void Dispose()
    {
    }
}
