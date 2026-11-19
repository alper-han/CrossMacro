namespace CrossMacro.Infrastructure.Tests.Abstractions;

public sealed class GlobalHotkeyServiceAbstractionsTests
{
    [Fact]
    public void RawHotkeyInputEventArgs_NullPressedModifiers_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new RawHotkeyInputEventArgs(1, null!, "F1"));
    }

    [Fact]
    public void RawHotkeyInputEventArgs_NullHotkeyString_ThrowsArgumentNullException()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new RawHotkeyInputEventArgs(1, new HashSet<int>(), null!));
    }

    [Fact]
    public async Task DefaultAsyncAdapters_HonorCancellationAndDelegateSynchronousStop()
    {
        var hotkeyService = new SynchronousOnlyHotkeyService();
        IGlobalHotkeyService service = hotkeyService;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await service.InitializeAsync(cancellation.Token));
        _ = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await service.StopHotkeyServiceAsync(cancellation.Token));
        Assert.False(hotkeyService.WasStopped);

        await service.StopHotkeyServiceAsync(TestContext.Current.CancellationToken);

        Assert.True(hotkeyService.WasStopped);
    }

    private sealed class SynchronousOnlyHotkeyService : IGlobalHotkeyService
    {
        public int RecordingHotkeyCode => -1;
        public int PlaybackHotkeyCode => -1;
        public int PauseHotkeyCode => -1;
        public bool IsRunning => false;
        public bool WasStopped { get; private set; }
        public string? LastError => null;

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

        public void Start() { }
        public void StopHotkeyService() => WasStopped = true;
        public void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey) { }
        public void ApplyHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey) { }
        public Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public void SetPlaybackPauseHotkeysEnabled(bool enabled) { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
