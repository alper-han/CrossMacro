using System;

namespace CrossMacro.Core.Services;

/// <summary>
/// Interface for global hotkey service
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>
    /// Fired when F8 is pressed (Toggle Recording)
    /// </summary>
    event EventHandler? ToggleRecordingRequested;
    
    /// <summary>
    /// Fired when F9 is pressed (Toggle Playback)
    /// </summary>
    event EventHandler? TogglePlaybackRequested;
    
    /// <summary>
    /// Fired when F10 is pressed (Toggle Pause)
    /// </summary>
    event EventHandler? TogglePauseRequested;
    
    /// <summary>
    /// Whether the service is currently running
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// Start monitoring keyboard devices for hotkeys
    /// </summary>
    void Start();
    
    /// <summary>
    /// Stop monitoring keyboard devices
    /// </summary>
    void Stop();
}
