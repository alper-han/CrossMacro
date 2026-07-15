namespace CrossMacro.Platform.Abstractions;

public interface IGlobalHotkeyService : IDisposable
{
    public int RecordingHotkeyCode { get; }
    public int PlaybackHotkeyCode { get; }
    public int PauseHotkeyCode { get; }
    public event EventHandler? ToggleRecordingRequested;

    public event EventHandler? TogglePlaybackRequested;

    public event EventHandler? TogglePauseRequested;

    /// <summary>
    /// Event fired for all key presses, allowing other services to listen
    /// </summary>
    public event EventHandler<RawHotkeyInputEventArgs>? RawInputReceived;

    /// <summary>
    /// Event fired when a key is released (same hotkey string as when pressed)
    /// </summary>
    public event EventHandler<RawHotkeyInputEventArgs>? RawKeyReleased;


    /// <summary>
    /// Event fired when a critical error occurs (e.g., daemon connection failure)
    /// </summary>
    public event EventHandler<GlobalHotkeyErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// The last critical error message encountered, if any.
    /// </summary>
    public string? LastError { get; }


    public bool IsRunning { get; }

    public void Start();

    public void StopHotkeyService();

    public void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey);

    public void ApplyHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey);

    public Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default);

    public void SetPlaybackPauseHotkeysEnabled(bool enabled);
}
