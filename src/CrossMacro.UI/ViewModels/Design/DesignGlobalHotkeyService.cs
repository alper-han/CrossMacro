
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignGlobalHotkeyService : IGlobalHotkeyService
{
    public int RecordingHotkeyCode => 19;

    public int PlaybackHotkeyCode => 25;

    public int PauseHotkeyCode => 57;

    public event EventHandler? ToggleRecordingRequested
    {
        add { /* Empty */ }
        remove { /* Empty */ }
    }

    public event EventHandler? TogglePlaybackRequested
    {
        add { /* Empty */ }
        remove { /* Empty */ }
    }

    public event EventHandler? TogglePauseRequested
    {
        add { /* Empty */ }
        remove { /* Empty */ }
    }

    public event EventHandler<RawHotkeyInputEventArgs>? RawInputReceived
    {
        add { /* Empty */ }
        remove { /* Empty */ }
    }

    public event EventHandler<RawHotkeyInputEventArgs>? RawKeyReleased
    {
        add { /* Empty */ }
        remove { /* Empty */ }
    }

    public event EventHandler<GlobalHotkeyErrorEventArgs>? ErrorOccurred
    {
        add { /* Empty */ }
        remove { /* Empty */ }
    }

    public string? LastError => null;

    public bool IsRunning { get; private set; }

    public static Task Completion => Task.CompletedTask;

    public void Start() => IsRunning = true;

    public void StopHotkeyService() => IsRunning = false;

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopHotkeyService();
        return Task.CompletedTask;
    }

    public void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey) { /* Empty */ }

    public void ApplyHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey) { /* Empty */ }

    public Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Ctrl+Alt+R");
    }

    public void SetPlaybackPauseHotkeysEnabled(bool enabled) { /* Empty */ }

    public void Dispose() { /* Empty */ }
}
