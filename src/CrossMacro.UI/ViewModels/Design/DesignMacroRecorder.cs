
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignMacroRecorder : IMacroRecorder
{
    public bool IsRecording { get; private set; }

    public event EventHandler<MacroEventRecordedEventArgs>? EventRecorded
    {
        add { /* Empty */ }
        remove { /* Empty */ }
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

    public void Dispose() { /* Empty */ }
}
