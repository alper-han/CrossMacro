namespace CrossMacro.Core.Services;

/// <summary>
/// Event args for raw input events forwarded from GlobalHotkeyService
/// </summary>
public class RawHotkeyInputEventArgs : EventArgs
{
    /// <summary>
    /// The key code that was pressed
    /// </summary>
    public int KeyCode { get; }
    
    /// <summary>
    /// Set of currently pressed modifier key codes
    /// </summary>
    public IReadOnlySet<int> PressedModifiers { get; }
    
    /// <summary>
    /// The full hotkey string (e.g., "Ctrl+Shift+P")
    /// </summary>
    public string HotkeyString { get; }
    
    public RawHotkeyInputEventArgs(int keyCode, IReadOnlySet<int> pressedModifiers, string hotkeyString)
    {
        KeyCode = keyCode;
        PressedModifiers = pressedModifiers;
        HotkeyString = hotkeyString;
    }
}

public interface IGlobalHotkeyService : IDisposable
{
    int RecordingHotkeyCode { get; }
    int PlaybackHotkeyCode { get; }
    int PauseHotkeyCode { get; }
    event EventHandler? ToggleRecordingRequested;
    
    event EventHandler? TogglePlaybackRequested;
    
    event EventHandler? TogglePauseRequested;
    
    /// <summary>
    /// Event fired for all key presses, allowing other services to listen
    /// </summary>
    event EventHandler<RawHotkeyInputEventArgs>? RawInputReceived;
    
    bool IsRunning { get; }
    
    void Start();
    
    void Stop();
    
    void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey);

    Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default);
    
    void SetPlaybackPauseHotkeysEnabled(bool enabled);
}
