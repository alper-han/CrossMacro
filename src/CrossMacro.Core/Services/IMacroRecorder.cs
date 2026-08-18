
namespace CrossMacro.Core.Services;

public interface IMacroRecorder : IDisposable
{
    public bool IsRecording { get; }

    public event EventHandler<MacroEventRecordedEventArgs>? EventRecorded;

    public Task StartRecordingAsync(bool recordMouse, bool recordKeyboard, IEnumerable<int>? ignoredKeys = null, bool forceRelative = false, bool skipInitialZero = false, CancellationToken cancellationToken = default);

    public MacroSequence StopRecording();

    public MacroSequence? GetCurrentRecording();
}
