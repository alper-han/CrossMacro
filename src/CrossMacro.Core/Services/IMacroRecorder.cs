
namespace CrossMacro.Core.Services;

public interface IMacroRecorder : IDisposable
{
    bool IsRecording { get; }

    event EventHandler<MacroEvent>? EventRecorded;

    Task StartRecordingAsync(bool recordMouse, bool recordKeyboard, IEnumerable<int>? ignoredKeys = null, bool forceRelative = false, bool skipInitialZero = false, CancellationToken cancellationToken = default);

    MacroSequence StopRecording();

    MacroSequence? GetCurrentRecording();
}
