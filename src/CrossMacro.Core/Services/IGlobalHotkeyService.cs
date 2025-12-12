namespace CrossMacro.Core.Services;

public interface IGlobalHotkeyService : IDisposable
{
    int RecordingHotkeyCode { get; }
    int PlaybackHotkeyCode { get; }
    int PauseHotkeyCode { get; }
    event EventHandler? ToggleRecordingRequested;
    
    event EventHandler? TogglePlaybackRequested;
    
    event EventHandler? TogglePauseRequested;
    
    bool IsRunning { get; }
    
    void Start();
    
    void Stop();
    
    void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey);

    Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default);
    
    void SetPlaybackPauseHotkeysEnabled(bool enabled);
}
