
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignGlobalHotkeyService : IGlobalHotkeyService
{
    public int RecordingHotkeyCode => 19;

    public int PlaybackHotkeyCode => 25;

    public int PauseHotkeyCode => 57;

    public event EventHandler? ToggleRecordingRequested
    {
        add { }
        remove { }
    }

    public event EventHandler? TogglePlaybackRequested
    {
        add { }
        remove { }
    }

    public event EventHandler? TogglePauseRequested
    {
        add { }
        remove { }
    }

    public event EventHandler<RawHotkeyInputEventArgs>? RawInputReceived
    {
        add { }
        remove { }
    }

    public event EventHandler<RawHotkeyInputEventArgs>? RawKeyReleased
    {
        add { }
        remove { }
    }

    public event EventHandler<GlobalHotkeyErrorEventArgs>? ErrorOccurred
    {
        add { }
        remove { }
    }

    public string? LastError => null;

    public bool IsRunning { get; private set; }

    public static Task Completion => Task.CompletedTask;

    public void Start() => IsRunning = true;

    public void StopHotkeyService() => IsRunning = false;

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        StopHotkeyService();
        return Task.CompletedTask;
    }

    public void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey)
    {
    }

    public void ApplyHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey)
    {
    }

    public Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Ctrl+Alt+R");
    }

    public void SetPlaybackPauseHotkeysEnabled(bool enabled)
    {
    }

    public void Dispose()
    {
    }
}
