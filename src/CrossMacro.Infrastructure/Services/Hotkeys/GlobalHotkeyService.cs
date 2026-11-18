
namespace CrossMacro.Infrastructure.Services.Hotkeys;

/// <summary>
/// Listens for the recording, playback and pause hotkeys and publishes raw key input for the
/// shortcut services.
/// </summary>
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RestartDelay = TimeSpan.FromMilliseconds(250);
    private const string InputCaptureRecoveryPrefix = "Recovery:";

    private readonly Lock _lock = new();
    private readonly InputCaptureLifecycle _captureLifecycle = new();
    private readonly IHotkeyConfigurationService _configService;
    private readonly HotkeyParser _hotkeyParser;
    private readonly HotkeyMatcher _hotkeyMatcher;
    private readonly ModifierStateTracker _modifierTracker;
    private readonly HotkeyStringBuilder _hotkeyStringBuilder;
    private readonly Func<IInputCapture>? _inputCaptureFactory;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly DebouncedSaveCoordinator _saveCoordinator;

    // Saves are keyed by file so a request captured for one profile is still written to that
    // profile's file if the active profile changes before the debounce elapses.
    private readonly Lock _pendingSavesLock = new();
    private readonly Dictionary<string, HotkeyConfigurationSaveRequest> _pendingSaves = new(StringComparer.Ordinal);

    // Written under _lock, read on the input thread without it: always through Volatile.
    private HotkeyBindings _bindings = HotkeyBindings.Empty;
    private TaskCompletionSource<string>? _captureTcs;
    private bool _playbackPauseHotkeysEnabled = true;

    private Task _restartTask = Task.CompletedTask;
    private bool _disposed;

    public event EventHandler? ToggleRecordingRequested;
    public event EventHandler? TogglePlaybackRequested;
    public event EventHandler? TogglePauseRequested;
    public event EventHandler<RawHotkeyInputEventArgs>? RawInputReceived;
    public event EventHandler<RawHotkeyInputEventArgs>? RawKeyReleased;
    public event EventHandler<GlobalHotkeyErrorEventArgs>? ErrorOccurred;

    public int RecordingHotkeyCode => Volatile.Read(ref _bindings).Recording.MainKey;
    public int PlaybackHotkeyCode => Volatile.Read(ref _bindings).Playback.MainKey;
    public int PauseHotkeyCode => Volatile.Read(ref _bindings).Pause.MainKey;
    public bool IsRunning { get; private set; }
    public string? LastError { get; private set; }

    public GlobalHotkeyService(
        IHotkeyConfigurationService configService,
        HotkeyParser hotkeyParser,
        HotkeyMatcher hotkeyMatcher,
        ModifierStateTracker modifierTracker,
        HotkeyStringBuilder hotkeyStringBuilder,
        Func<IInputCapture>? inputCaptureFactory = null,
        HotkeySettings? initialSettings = null,
        TimeProvider? timeProvider = null)
    {
        _configService = configService;
        _hotkeyParser = hotkeyParser;
        _hotkeyMatcher = hotkeyMatcher;
        _modifierTracker = modifierTracker;
        _hotkeyStringBuilder = hotkeyStringBuilder;
        _inputCaptureFactory = inputCaptureFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _saveCoordinator = new DebouncedSaveCoordinator(SavePendingAsync, SaveDebounce, _timeProvider);

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
            CancelPendingCapture(CancellationToken.None);

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
        bool shouldLog;
        using (_lock.EnterScope())
        {
            CancelPendingCapture(cancellationToken);
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
            var previous = _bindings;
            var mappingsChanged = !string.Equals(previous.RecordingText, recordingHotkey, StringComparison.Ordinal)
                || !string.Equals(previous.PlaybackText, playbackHotkey, StringComparison.Ordinal)
                || !string.Equals(previous.PauseText, pauseHotkey, StringComparison.Ordinal);

            Volatile.Write(ref _bindings, new HotkeyBindings(
                _hotkeyParser.Parse(recordingHotkey),
                _hotkeyParser.Parse(playbackHotkey),
                _hotkeyParser.Parse(pauseHotkey),
                recordingHotkey,
                playbackHotkey,
                pauseHotkey));

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
            EnqueueSave(_configService.CaptureSaveRequest(new HotkeySettings
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

            Volatile.Write(ref _captureTcs, captureTcs);
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
                        Volatile.Write(ref _captureTcs, null);
                    }
                }
            }
        }
    }

    public void SetPlaybackPauseHotkeysEnabled(bool enabled)
    {
        Volatile.Write(ref _playbackPauseHotkeysEnabled, enabled);
        Log.Information("[GlobalHotkeyService] Playback/Pause hotkeys {Status}", enabled ? "enabled" : "disabled");
    }

    /// <summary>Stops listening and writes any pending hotkey save without blocking the caller.</summary>
    public async ValueTask DisposeAsync()
    {
        using (_lock.EnterScope())
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        await _disposeCts.CancelAsync().ConfigureAwait(false);
        await StopHotkeyServiceAsync().ConfigureAwait(false);
        await _saveCoordinator.DisposeAsync().ConfigureAwait(false);
        _disposeCts.Dispose();
    }

    private void CancelPendingCapture(CancellationToken cancellationToken)
    {
        var pending = Interlocked.Exchange(ref _captureTcs, value: null);
        _ = pending?.TrySetCanceled(cancellationToken);
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
        if (e.Value is 0)
        {
            // Broadcast releases, including modifiers, for held and press/release shortcuts.
            RawKeyReleased?.Invoke(this, new RawHotkeyInputEventArgs(e.Code, _modifierTracker.CurrentModifiers, string.Empty));
            _modifierTracker.OnKeyReleased(e.Code);
            return;
        }

        if (e.Value is not 1)
        {
            return;
        }

        _modifierTracker.OnKeyPressed(e.Code);
        var currentModifiers = _modifierTracker.CurrentModifiers;
        Log.Debug("[GlobalHotkeyService] Key pressed: Code={Code}, CurrentModifiers=[{Modifiers}]",
            e.Code, string.Join('+', currentModifiers));

        // A modifier key on its own is never a hotkey.
        if (currentModifiers.Contains(e.Code))
        {
            return;
        }

        if (IsBareMouseClick(e.Code))
        {
            return;
        }

        var hotkeyString = _hotkeyStringBuilder.Build(e.Code, currentModifiers);
        Log.Debug("[GlobalHotkeyService] Hotkey candidate: {HotkeyString} (Code={Code})", hotkeyString, e.Code);
        Dispatch(e.Code, currentModifiers, hotkeyString);
    }

    private void HandleMouseButtonInput(CapturedInputEvent e)
    {
        var currentModifiers = _modifierTracker.CurrentModifiers;

        if (e.Value is 0)
        {
            // Broadcast mouse releases for held and press/release shortcuts.
            RawKeyReleased?.Invoke(this, new RawHotkeyInputEventArgs(e.Code, currentModifiers, string.Empty));
            return;
        }

        if (e.Value is not 1 || IsBareMouseClick(e.Code))
        {
            return;
        }

        var mouseButtonName = MouseButtonMapper.GetMouseButtonName(e.Code);
        if (string.IsNullOrEmpty(mouseButtonName))
        {
            return;
        }

        Dispatch(e.Code, currentModifiers, HotkeyStringBuilder.BuildForMouse(mouseButtonName, currentModifiers));
    }

    // Plain left and right clicks must keep working, so they only count with a modifier held.
    private bool IsBareMouseClick(int code) =>
        code is InputEventCode.BTN_LEFT or InputEventCode.BTN_RIGHT && !_modifierTracker.HasModifiers;

    private void Dispatch(int code, IReadOnlySet<int> modifiers, string hotkeyString)
    {
        if (Volatile.Read(ref _captureTcs) is { } captureTcs)
        {
            Log.Debug("[GlobalHotkeyService] Captured hotkey: {HotkeyString}", hotkeyString);
            _ = captureTcs.TrySetResult(hotkeyString);
            return;
        }

        var bindings = Volatile.Read(ref _bindings);
        if (_hotkeyMatcher.TryMatch(code, modifiers, bindings.Recording, "Recording"))
        {
            Log.Information("[GlobalHotkeyService] Recording Hotkey Pressed: {Hotkey}", hotkeyString);
            ToggleRecordingRequested?.Invoke(this, EventArgs.Empty);
        }

        if (Volatile.Read(ref _playbackPauseHotkeysEnabled))
        {
            if (_hotkeyMatcher.TryMatch(code, modifiers, bindings.Playback, "Playback"))
            {
                Log.Information("[GlobalHotkeyService] Playback Hotkey Pressed: {Hotkey}", hotkeyString);
                TogglePlaybackRequested?.Invoke(this, EventArgs.Empty);
            }

            if (_hotkeyMatcher.TryMatch(code, modifiers, bindings.Pause, "Pause"))
            {
                Log.Information("[GlobalHotkeyService] Pause Hotkey Pressed: {Hotkey}", hotkeyString);
                TogglePauseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        RawInputReceived?.Invoke(this, new RawHotkeyInputEventArgs(code, modifiers, hotkeyString));
    }

    private void OnInputCaptureError(object? sender, InputCaptureErrorEventArgs e)
    {
        var errorMessage = e.Message;
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

            if (!InputBackendErrorClassifier.IsKnownUnavailableMessage(errorMessage) && _restartTask.IsCompleted)
            {
                _restartTask = RestartCaptureAsync(errorMessage);
            }
        }

        if (shouldNotify)
        {
            ErrorOccurred?.Invoke(this, new GlobalHotkeyErrorEventArgs(errorMessage));
        }
    }

    private void EnqueueSave(HotkeyConfigurationSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using (_pendingSavesLock.EnterScope())
        {
            _pendingSaves[request.ConfigPath] = request;
        }

        try
        {
            _ = _saveCoordinator.RequestAsync();
        }
        catch (ObjectDisposedException)
        {
            ReportPersistenceFailure("Hotkey configuration save was discarded because the service is shutting down.");
        }
    }

    private async Task SavePendingAsync()
    {
        HotkeyConfigurationSaveRequest[] batch;
        using (_pendingSavesLock.EnterScope())
        {
            batch = [.. _pendingSaves.Values];
            _pendingSaves.Clear();
        }

        foreach (var request in batch)
        {
            try
            {
                if (!await _configService.TrySaveAsync(request).ConfigureAwait(false))
                {
                    ReportPersistenceFailure($"Failed to save hotkey configuration to '{request.ConfigPath}'.");
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "Failed to save hotkey configuration asynchronously");
                ReportPersistenceFailure($"Failed to save hotkey configuration to '{request.ConfigPath}': {ex.Message}");
            }
        }
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

    private async Task RestartCaptureAsync(string cause)
    {
        try
        {
            await Task.Delay(RestartDelay, _timeProvider, _disposeCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

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

    private sealed record HotkeyBindings(
        HotkeyMapping Recording,
        HotkeyMapping Playback,
        HotkeyMapping Pause,
        string RecordingText,
        string PlaybackText,
        string PauseText)
    {
        public static HotkeyBindings Empty { get; } = new(new(), new(), new(), string.Empty, string.Empty, string.Empty);
    }
}
