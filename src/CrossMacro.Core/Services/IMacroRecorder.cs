using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Interface for macro recording service
/// </summary>
public interface IMacroRecorder : IDisposable
{
    /// <summary>
    /// Whether recording is currently active
    /// </summary>
    bool IsRecording { get; }
    
    /// <summary>
    /// Event fired when a new event is recorded
    /// </summary>
    event EventHandler<MacroEvent>? EventRecorded;

    /// <summary>
    /// Starts recording events from detected devices
    /// </summary>
    /// <param name="recordMouse">Whether to record mouse events</param>
    /// <param name="recordKeyboard">Whether to record keyboard events</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the recording operation</returns>
    Task StartRecordingAsync(bool recordMouse, bool recordKeyboard, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops recording and returns the recorded sequence
    /// </summary>
    /// <returns>The recorded macro sequence</returns>
    MacroSequence StopRecording();
    
    /// <summary>
    /// Gets the current recording state
    /// </summary>
    MacroSequence? GetCurrentRecording();
}
