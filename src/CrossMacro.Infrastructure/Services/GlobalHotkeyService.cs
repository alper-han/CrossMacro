
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Manages global hotkeys for recording, playback, and pause actions.
/// Refactored to delegate responsibilities to specialized services.
/// </summary>
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private bool _disposed;
    private readonly Lock _lock = new();
    private int _restartInProgress;
    private readonly InputCaptureLifecycle _captureLifecycle = new();
    private const string InputCaptureRecoveryPrefix = "Recovery:";

    // Injected services
    private readonly IHotkeyConfigurationService _configService;
    private readonly IHotkeyParser _hotkeyParser;
    private readonly IHotkeyMatcher _hotkeyMatcher;
    private readonly IModifierStateTracker _modifierTracker;
    private readonly IHotkeyStringBuilder _hotkeyStringBuilder;
    private readonly IMouseButtonMapper _mouseButtonMapper;
    private readonly Func<IInputCapture>? _inputCaptureFactory;
    private readonly HotkeyPersistenceQueue _persistenceQueue;

    // Hotkey mappings
    private HotkeyMapping _recordingHotkey = new();
    private HotkeyMapping _playbackHotkey = new();
    private HotkeyMapping _pauseHotkey = new();
    private string _recordingHotkeyString = string.Empty;
    private string _playbackHotkeyString = string.Empty;
    private string _pauseHotkeyString = string.Empty;

    private bool _playbackPauseHotkeysEnabled = true;

    // Events
    public event EventHandler? ToggleRecordingRequested;
    public event EventHandler? TogglePlaybackRequested;
    public event EventHandler? TogglePauseRequested;
    public event EventHandler<RawHotkeyInputEventArgs>? RawInputReceived;
    public event EventHandler<RawHotkeyInputEventArgs>? RawKeyReleased;
    public event EventHandler<GlobalHotkeyErrorEventArgs>? ErrorOccurred;

    // Properties
    public int RecordingHotkeyCode => _recordingHotkey.MainKey;
    public int PlaybackHotkeyCode => _playbackHotkey.MainKey;
    public int PauseHotkeyCode => _pauseHotkey.MainKey;
    public bool IsRunning { get; private set; }
    public string? LastError { get; private set; }

    // Capture mode
    private TaskCompletionSource<string>? _captureTcs;
    private bool _isCapturing;

    public GlobalHotkeyService(
        IHotkeyConfigurationService configService,
        IHotkeyParser hotkeyParser,
        IHotkeyMatcher hotkeyMatcher,
        IModifierStateTracker modifierTracker,
        IHotkeyStringBuilder hotkeyStringBuilder,
        IMouseButtonMapper mouseButtonMapper,
        Func<IInputCapture>? inputCaptureFactory = null,
        HotkeySettings? initialSettings = null)
    {
        _configService = configService;
        _hotkeyParser = hotkeyParser;
        _hotkeyMatcher = hotkeyMatcher;
        _modifierTracker = modifierTracker;
        _hotkeyStringBuilder = hotkeyStringBuilder;
        _mouseButtonMapper = mouseButtonMapper;
        _inputCaptureFactory = inputCaptureFactory;
        _persistenceQueue = new HotkeyPersistenceQueue(_configService, ReportPersistenceFailure);

        var settings = initialSettings ?? _configService.Load();
        UpdateHotkeys(settings.RecordingHotkey, settings.PlaybackHotkey, settings.PauseHotkey, save: false);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = await _configService.LoadAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        UpdateHotkeys(settings.RecordingHotkey, settings.PlaybackHotkey, settings.PauseHotkey, save: false);
    }

    public void Start()
    {
        using (_lock.EnterScope())
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (IsRunning)
            {
                return;
            }

            if (_inputCaptureFactory is null)
            {
                throw new InvalidOperationException("No input capture factory configured");
            }

            IsRunning = true;

            try
            {
                StartCapture_NoLock();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                IsRunning = false;
                CleanupCapture_NoLock();
                throw;
            }
        }
    }

    public void StopHotkeyService()
    {
        using (_lock.EnterScope())
        {
            _ = _captureTcs?.TrySetCanceled();
            _captureTcs = null;
            _isCapturing = false;

            if (!IsRunning && !_captureLifecycle.HasActiveResources)
            {
                return;
            }

            var wasRunning = IsRunning;
            IsRunning = false;
            CleanupCapture_NoLock();
            if (wasRunning)
            {
                Log.Information("[GlobalHotkeyService] Stopped");
            }
        }
    }

    public async Task StopHotkeyServiceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var shouldLog = false;
        using (_lock.EnterScope())
        {
            _ = _captureTcs?.TrySetCanceled(cancellationToken);
            _captureTcs = null;
            _isCapturing = false;
            shouldLog = IsRunning;
            IsRunning = false;
        }

        if (_captureLifecycle.HasActiveResources)
        {
            await _captureLifecycle.CleanupAsync(
                OnInputReceived,
                OnInputCaptureError,
                ex => Log.LogError(ex, "[GlobalHotkeyService] Error stopping input capture")).ConfigureAwait(false);
        }

        if (shouldLog)
        {
            Log.Information("[GlobalHotkeyService] Stopped");
        }
    }

    public void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey)
    {
        UpdateHotkeys(recordingHotkey, playbackHotkey, pauseHotkey, save: true);
    }

    public void ApplyHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey)
    {
        UpdateHotkeys(recordingHotkey, playbackHotkey, pauseHotkey, save: false);
    }

    private void UpdateHotkeys(string recordingHotkey, string playbackHotkey, string pauseHotkey, bool save)
    {
        using (_lock.EnterScope())
        {
            var mappingsChanged = !string.Equals(_recordingHotkeyString, recordingHotkey, StringComparison.Ordinal)
                || !string.Equals(_playbackHotkeyString, playbackHotkey, StringComparison.Ordinal)
                || !string.Equals(_pauseHotkeyString, pauseHotkey, StringComparison.Ordinal);

            _recordingHotkey = _hotkeyParser.Parse(recordingHotkey);
            _playbackHotkey = _hotkeyParser.Parse(playbackHotkey);
            _pauseHotkey = _hotkeyParser.Parse(pauseHotkey);
            _recordingHotkeyString = recordingHotkey;
            _playbackHotkeyString = playbackHotkey;
            _pauseHotkeyString = pauseHotkey;

            if (mappingsChanged)
            {
                Log.Information("[GlobalHotkeyService] Updated hotkeys: Recording={Recording}, Playback={Playback}, Pause={Pause}",
                    recordingHotkey, playbackHotkey, pauseHotkey);
            }
            else
            {
                Log.Debug("[GlobalHotkeyService] Hotkeys unchanged");
            }
        }

        if (save)
        {
            _persistenceQueue.Enqueue(_configService.CaptureSaveRequest(new HotkeySettings
            {
                RecordingHotkey = recordingHotkey,
                PlaybackHotkey = playbackHotkey,
                PauseHotkey = pauseHotkey,
            }));
        }
    }

    public async Task<string> CaptureNextKeyAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            throw new InvalidOperationException(LastError ?? "Global hotkey capture is not running.");
        }

        if (_inputCaptureFactory is null)
        {
            throw new InvalidOperationException("No input capture factory configured");
        }

        var captureTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (_lock.EnterScope())
        {
            if (_captureTcs is not null)
            {
                throw new InvalidOperationException("A hotkey capture is already in progress.");
            }

            _captureTcs = captureTcs;
            _isCapturing = true;
        }

        _modifierTracker.Clear();

        using (cancellationToken.Register(() => captureTcs.TrySetCanceled(cancellationToken)))
        {
            try
            {
                return await captureTcs.Task.ConfigureAwait(false);
            }
            finally
            {
                using (_lock.EnterScope())
                {
                    if (ReferenceEquals(_captureTcs, captureTcs))
                    {
                        _isCapturing = false;
                        _captureTcs = null;
                    }
                }
            }
        }
    }

    private void OnInputReceived(object? sender, CapturedInputEventArgs e)
    {
        var inputEvent = e.Event;
        if (inputEvent.Type is InputEventType.Key)
        {
            HandleKeyboardInput(inputEvent);
            return;
        }

        if (inputEvent.Type is InputEventType.MouseButton)
        {
            HandleMouseButtonInput(inputEvent);
        }
    }

    private void HandleKeyboardInput(CapturedInputEvent e)
    {
        if (e.Value is 1)
        {
            _modifierTracker.OnKeyPressed(e.Code);
            Log.Debug("[GlobalHotkeyService] Key pressed: Code={Code}, CurrentModifiers=[{Modifiers}]",
                e.Code, string.Join('+', _modifierTracker.CurrentModifiers));
        }
        else if (e.Value is 0)
        {
            var releaseModifiers = _modifierTracker.CurrentModifiers;
            // Always fire RawKeyReleased for all keys (including modifiers)
            // so RunWhileHeld shortcuts can stop when any part of the hotkey is released
            RawKeyReleased?.Invoke(this, new RawHotkeyInputEventArgs(e.Code, releaseModifiers, string.Empty));
            _modifierTracker.OnKeyReleased(e.Code);
        }

        if (e.Value is not 1)
        {
            return;
        }

        // Skip if this is a modifier key
        var currentModifiers = _modifierTracker.CurrentModifiers;
        if (currentModifiers.Contains(e.Code))
        {
            return;
        }

        // Block pure mouse left (BTN_LEFT) and right (BTN_RIGHT) clicks without modifiers
        if ((e.Code == InputEventCode.BTN_LEFT || e.Code == InputEventCode.BTN_RIGHT) && !_modifierTracker.HasModifiers)
        {
            return;
        }

        // Build hotkey string
        var hotkeyString = _hotkeyStringBuilder.Build(e.Code, currentModifiers);

        Log.Debug("[GlobalHotkeyService] Hotkey candidate: {HotkeyString} (Code={Code})", hotkeyString, e.Code);

        if (_isCapturing && _captureTcs is not null)
        {
            var captureTcs = _captureTcs;
            Log.Debug("[GlobalHotkeyService] Captured hotkey: {HotkeyString}", hotkeyString);
            _ = captureTcs.TrySetResult(hotkeyString);
            return;
        }

        // Check hotkey matches
        if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _recordingHotkey, "Recording"))
        {
            Log.Information("[GlobalHotkeyService] Recording Hotkey Pressed: {Hotkey}", hotkeyString);
            ToggleRecordingRequested?.Invoke(this, EventArgs.Empty);
        }

        if (_playbackPauseHotkeysEnabled)
        {
            if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _playbackHotkey, "Playback"))
            {
                Log.Information("[GlobalHotkeyService] Playback Hotkey Pressed: {Hotkey}", hotkeyString);
                TogglePlaybackRequested?.Invoke(this, EventArgs.Empty);
            }

            if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _pauseHotkey, "Pause"))
            {
                Log.Information("[GlobalHotkeyService] Pause Hotkey Pressed: {Hotkey}", hotkeyString);
                TogglePauseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        // Broadcast raw input
        RawInputReceived?.Invoke(this, new RawHotkeyInputEventArgs(e.Code, currentModifiers, hotkeyString));
    }

    private void HandleMouseButtonInput(CapturedInputEvent e)
    {
        var currentModifiers = _modifierTracker.CurrentModifiers;

        if (e.Value is 0)
        {
            // Always fire RawKeyReleased for mouse buttons
            // so RunWhileHeld shortcuts can stop when the button is released
            RawKeyReleased?.Invoke(this, new RawHotkeyInputEventArgs(e.Code, currentModifiers, string.Empty));
            return;
        }

        if (e.Value is not 1)
        {
            return;
        }

        // Block pure left/right click without modifiers
        if ((e.Code == InputEventCode.BTN_LEFT || e.Code == InputEventCode.BTN_RIGHT) && !_modifierTracker.HasModifiers)
        {
            return;
        }

        var mouseButtonName = _mouseButtonMapper.GetMouseButtonName(e.Code);
        if (string.IsNullOrEmpty(mouseButtonName))
        {
            return;
        }

        var hotkeyString = _hotkeyStringBuilder.BuildForMouse(mouseButtonName, currentModifiers);

        if (_isCapturing && _captureTcs is not null)
        {
            var captureTcs = _captureTcs;
            _ = captureTcs.TrySetResult(hotkeyString);
            return;
        }

        // Check hotkey matches
        if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _recordingHotkey, "Recording"))
        {
            Log.Information("[GlobalHotkeyService] Recording Hotkey Pressed");
            ToggleRecordingRequested?.Invoke(this, EventArgs.Empty);
        }

        if (_playbackPauseHotkeysEnabled)
        {
            if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _playbackHotkey, "Playback"))
            {
                Log.Information("[GlobalHotkeyService] Playback Hotkey Pressed");
                TogglePlaybackRequested?.Invoke(this, EventArgs.Empty);
            }

            if (_hotkeyMatcher.TryMatch(e.Code, currentModifiers, _pauseHotkey, "Pause"))
            {
                Log.Information("[GlobalHotkeyService] Pause Hotkey Pressed");
                TogglePauseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        RawInputReceived?.Invoke(this, new RawHotkeyInputEventArgs(e.Code, currentModifiers, hotkeyString));
    }

    private void OnInputCaptureError(object? sender, InputCaptureErrorEventArgs e)
    {
        var errorMessage = e.Message;
        var shouldRestart = false;
        var shouldNotify = false;

        using (_lock.EnterScope())
        {
            if (InputBackendErrorClassifier.IsKnownUnavailableMessage(errorMessage))
            {
                Log.Warning("[GlobalHotkeyService] Input capture unavailable: {Error}", errorMessage);
            }
            else
            {
                Log.LogError("[GlobalHotkeyService] Input capture error: {Error}", errorMessage);
            }

            LastError = errorMessage;
            shouldNotify = !errorMessage.StartsWith(InputCaptureRecoveryPrefix, StringComparison.Ordinal);

            if (!IsRunning ||
                (sender is IInputCapture capture && !_captureLifecycle.IsCurrent(capture)))
            {
                return;
            }

            shouldRestart = true;
        }

        if (shouldNotify)
        {
            ErrorOccurred?.Invoke(this, new GlobalHotkeyErrorEventArgs(errorMessage));
        }

        if (shouldRestart)
        {
            _ = TryRestartCaptureAsync(errorMessage);
        }
    }

    public void SetPlaybackPauseHotkeysEnabled(bool enabled)
    {
        using (_lock.EnterScope())
        {
            _playbackPauseHotkeysEnabled = enabled;
            Log.Information("[GlobalHotkeyService] Playback/Pause hotkeys {Status}", enabled ? "enabled" : "disabled");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopHotkeyService();
        _persistenceQueue.Dispose();
    }

    private void ReportPersistenceFailure(string errorMessage)
    {
        using (_lock.EnterScope())
        {
            LastError = errorMessage;
        }

        ErrorOccurred?.Invoke(this, new GlobalHotkeyErrorEventArgs(errorMessage));
    }

    private void StartCapture_NoLock()
    {
        _captureLifecycle.Start(
            _inputCaptureFactory!,
            captureMouse: true,
            captureKeyboard: true,
            OnInputReceived,
            OnInputCaptureError,
            OnCaptureStarted,
            OnCaptureFaulted);
    }

    private void CleanupCapture_NoLock()
    {
        _captureLifecycle.Cleanup(
            OnInputReceived,
            OnInputCaptureError,
            ex => Log.LogError(ex, "[GlobalHotkeyService] Error stopping input capture"));
    }

    private void OnCaptureStarted(IInputCapture capture)
    {
        using (_lock.EnterScope())
        {
            if (IsRunning && _captureLifecycle.IsCurrent(capture))
            {
                Log.Information("[GlobalHotkeyService] Started via {ProviderName}", capture.ProviderName);
            }
        }
    }

    private void OnCaptureFaulted(IInputCapture capture, Exception ex)
    {
        bool shouldReport;
        using (_lock.EnterScope())
        {
            shouldReport = IsRunning && _captureLifecycle.IsCurrent(capture);
            if (!shouldReport)
            {
                return;
            }

            LastError = ex.Message;
            IsRunning = false;
            CleanupCapture_NoLock();
        }

        Log.LogError(ex, "[GlobalHotkeyService] Input capture failed during startup");
        ErrorOccurred?.Invoke(this, new GlobalHotkeyErrorEventArgs(ex.Message));
    }

    private async Task TryRestartCaptureAsync(string cause)
    {
        if (Interlocked.CompareExchange(ref _restartInProgress, 1, 0) is not 0)
        {
            return;
        }

        try
        {
            await Task.Delay(250, CancellationToken.None).ConfigureAwait(false);

            using (_lock.EnterScope())
            {
                if (!IsRunning || _inputCaptureFactory is null)
                {
                    return;
                }

                Log.Warning("[GlobalHotkeyService] Restarting input capture after error: {Cause}", cause);

                try
                {
                    CleanupCapture_NoLock();
                    _modifierTracker.Clear();
                    StartCapture_NoLock();
                    LastError = null;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    CleanupCapture_NoLock();
                    IsRunning = false;
                    LastError = $"Restart failed: {ex.Message}";
                    ErrorOccurred?.Invoke(this, new GlobalHotkeyErrorEventArgs(LastError));
                    Log.LogError(ex, "[GlobalHotkeyService] Failed to restart input capture");
                }
            }
        }
        finally
        {
            _ = Interlocked.Exchange(ref _restartInProgress, 0);
        }
    }

    private sealed class HotkeyPersistenceQueue : IDisposable
    {
        private readonly IHotkeyConfigurationService _configService;
        private readonly Action<string> _reportFailure;
        private readonly Channel<HotkeyConfigurationSaveRequest> _requests = Channel.CreateUnbounded<HotkeyConfigurationSaveRequest>();
        private readonly Task _worker;
        private int _disposed;

        public HotkeyPersistenceQueue(IHotkeyConfigurationService configService, Action<string> reportFailure)
        {
            _configService = configService;
            _reportFailure = reportFailure;
            _worker = Task.Run(ProcessAsync, CancellationToken.None);
        }

        public void Enqueue(HotkeyConfigurationSaveRequest request)
        {
            if (Volatile.Read(ref _disposed) is not 0 || !_requests.Writer.TryWrite(request))
            {
                _reportFailure("Hotkey configuration save was discarded because the service is shutting down.");
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) is not 0)
            {
                return;
            }

            _ = _requests.Writer.TryComplete();
            _worker.GetAwaiter().GetResult();
        }

        private async Task ProcessAsync()
        {
            await foreach (var request in _requests.Reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
            {
                try
                {
                    var saved = await _configService.TrySaveAsync(request).ConfigureAwait(false);
                    if (!saved)
                    {
                        _reportFailure($"Failed to save hotkey configuration to '{request.ConfigPath}'.");
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.LogError(ex, "Failed to save hotkey configuration asynchronously");
                    _reportFailure($"Failed to save hotkey configuration to '{request.ConfigPath}': {ex.Message}");
                }
            }
        }
    }
}
