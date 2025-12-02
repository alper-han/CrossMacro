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
    
    /// <summary>
    /// Update hotkey mappings dynamically
    /// </summary>
    /// <param name="recordingHotkey">Hotkey for recording (e.g., "F8", "J", "Super+J")</param>
    /// <param name="playbackHotkey">Hotkey for playback</param>
    /// <param name="pauseHotkey">Hotkey for pause</param>
    void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey);

    /// <summary>
    /// Captures the next key press (with modifiers) and returns the hotkey string
    /// </summary>
    /// <returns>Hotkey string (e.g. "F8", "Ctrl+J")</returns>
    Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Enable or disable playback and pause hotkeys (used during recording to allow recording these keys)
    /// </summary>
    /// <param name="enabled">True to enable, false to disable</param>
    void SetPlaybackPauseHotkeysEnabled(bool enabled);
}
