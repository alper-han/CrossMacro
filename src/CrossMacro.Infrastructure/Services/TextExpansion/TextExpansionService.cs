
namespace CrossMacro.Infrastructure.Services.TextExpansion;

/// <summary>
/// Service for monitoring keystrokes and performing text expansion.
/// Refactored to coordinate InputProcessor, BufferState, and Executor.
/// </summary>
public sealed class TextExpansionService : ITextExpansionService
{
    private readonly ISettingsService _settingsService;
    private readonly TimeProvider _timeProvider;
    private readonly ITextExpansionStorageService _storageService;
    private readonly Func<IInputCapture> _inputCaptureFactory;

    // Decomposed Components
    private readonly IInputProcessor _inputProcessor;
    private readonly ITextBufferState _bufferState;
    private readonly ITextExpansionExecutor _startExecutor;

    // Lifecycle management
    private readonly Lock _lock;
    private readonly SemaphoreSlim _expansionLock;
    private bool _expansionInProgress;
    private CancellationTokenSource? _expansionCancellation;
    private bool _disposed;
    private bool _asyncStartupInProgress;
    private CancellationTokenSource? _asyncStartupCancellation;
    private long _startupGeneration;
    private readonly InputCaptureLifecycle _captureLifecycle;
    private int _lastCharacterKeyCode;
    private CancellationTokenSource? _restartCancellation;

    internal Task? ExpansionTask { get; private set; }
    internal Task? RestartTask { get; private set; }

    public bool IsRunning { get; private set; }

    public TextExpansionService(
        ISettingsService settingsService,
        ITextExpansionStorageService storageService,
        Func<IInputCapture> inputCaptureFactory,
        IInputProcessor inputProcessor,
        ITextBufferState bufferState,
        ITextExpansionExecutor startExecutor,
        TimeProvider? timeProvider = null)
    {
        _settingsService = settingsService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _storageService = storageService;
        _inputCaptureFactory = inputCaptureFactory;

        _inputProcessor = inputProcessor;
        _bufferState = bufferState;
        _startExecutor = startExecutor;

        _lock = new Lock();
        _expansionLock = new SemaphoreSlim(1, 1);
        _captureLifecycle = new InputCaptureLifecycle();

        // Subscribe to Processor events
        _inputProcessor.CharacterReceived += OnCharacterReceived;
        _inputProcessor.SpecialKeyReceived += OnSpecialKeyReceived;
    }

    public void Start()
    {
        if (!_settingsService.Current.EnableTextExpansion)
        {
            Log.Information("[TextExpansionService] Not starting because feature is disabled");
            return;
        }

        lock (_lock)
        {
            if (_disposed || IsRunning)
            {
                return;
            }

            _startupGeneration++;
            _asyncStartupInProgress = false;

            try
            {
                if (!_storageService.IsLoaded)
                {
                    _ = _storageService.Load();
                }

                StartCaptureSession_NoLock();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[TextExpansionService] Failed to start");
                CleanupCapture_NoLock();
                IsRunning = false;
            }
        }
    }

    /// <summary>
    /// Starts the capture session and resets input-processing state. Caller must hold <see cref="_lock"/>.
    /// </summary>
    private void StartCaptureSession_NoLock()
    {
        IsRunning = true;
        _captureLifecycle.Start(
            _inputCaptureFactory,
            captureMouse: false,
            captureKeyboard: true,
            OnInputReceived,
            OnInputCaptureError,
            OnCaptureStarted,
            OnCaptureFaulted);

        // Reset State
        _inputProcessor.Reset();
        _bufferState.Clear();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settingsService.Current.EnableTextExpansion)
        {
            Log.Information("[TextExpansionService] Not starting because feature is disabled");
            return;
        }

        using var startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        long startupGeneration;
        lock (_lock)
        {
            if (_disposed || IsRunning || _asyncStartupInProgress)
            {
                return;
            }

            startupGeneration = ++_startupGeneration;
            _asyncStartupInProgress = true;
            _asyncStartupCancellation = startupCancellation;
        }

        try
        {
            startupCancellation.Token.ThrowIfCancellationRequested();
            if (!_storageService.IsLoaded)
            {
                _ = await _storageService.LoadAsync().ConfigureAwait(false);
            }
            startupCancellation.Token.ThrowIfCancellationRequested();

            lock (_lock)
            {
                if (!_asyncStartupInProgress ||
                    startupGeneration != _startupGeneration ||
                    _disposed ||
                    IsRunning)
                {
                    return;
                }

                if (!_settingsService.Current.EnableTextExpansion)
                {
                    _asyncStartupInProgress = false;
                    Log.Information("[TextExpansionService] Not starting because feature is disabled");
                    return;
                }

                if (startupCancellation.IsCancellationRequested)
                {
                    _asyncStartupInProgress = false;
                    startupCancellation.Token.ThrowIfCancellationRequested();
                }

            }

            await _captureLifecycle.StartAsync(
                _inputCaptureFactory,
                captureMouse: false,
                captureKeyboard: true,
                OnInputReceived,
                OnInputCaptureError,
                OnCaptureStarted,
                OnCaptureFaulted,
                startupCancellation.Token).ConfigureAwait(false);

            lock (_lock)
            {
                if (startupGeneration == _startupGeneration &&
                    _asyncStartupInProgress &&
                    !_disposed)
                {
                    _inputProcessor.Reset();
                    _bufferState.Clear();
                    _lastCharacterKeyCode = 0;
                    IsRunning = true;
                }
                else
                {
                    CleanupCapture_NoLock();
                }
            }

            lock (_lock)
            {
                if (startupGeneration == _startupGeneration)
                {
                    _asyncStartupInProgress = false;
                    _asyncStartupCancellation = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
            lock (_lock)
            {
                if (startupGeneration == _startupGeneration)
                {
                    _asyncStartupInProgress = false;
                    _asyncStartupCancellation = null;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[TextExpansionService] Failed to start");
            lock (_lock)
            {
                if (startupGeneration == _startupGeneration)
                {
                    _asyncStartupInProgress = false;
                }
            }
        }
        catch (OutOfMemoryException)
        {
            lock (_lock)
            {
                if (startupGeneration == _startupGeneration)
                {
                    CleanupCapture_NoLock();
                    IsRunning = false;
                    _asyncStartupInProgress = false;
                    _asyncStartupCancellation = null;
                }
            }

            throw;
        }
    }

    public void StopExpansion()
    {
        _ = CompleteStopExpansionAsync(BeginStopExpansion());
    }

    public async Task StopExpansionAsync(CancellationToken cancellationToken = default)
    {
        var completionTask = CompleteStopExpansionAsync(BeginStopExpansion());
        await completionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private (Task? StartupCancellationTask, Task? ExpansionCancellationTask, Task? ExpansionTask, Task? RestartCancellationTask, Task? RestartTask) BeginStopExpansion()
    {
        lock (_lock)
        {
            _startupGeneration++;
            var restartCancellationTask = _restartCancellation?.CancelAsync();
            var restartTask = RestartTask;
            // A new capture lifetime may request recovery before this attempt settles.
            _restartCancellation = null;
            var startupInProgress = _asyncStartupInProgress;
            _asyncStartupInProgress = false;
            Task? startupCancellationTask = null;
            try
            {
                startupCancellationTask = _asyncStartupCancellation?.CancelAsync();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[TextExpansionService] Error canceling startup");
            }

            _asyncStartupCancellation = null;
            Task? expansionCancellationTask = null;
            try
            {
                expansionCancellationTask = _expansionCancellation?.CancelAsync();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[TextExpansionService] Error canceling expansion");
            }

            var expansionTask = ExpansionTask;
            if (!IsRunning && !_captureLifecycle.HasActiveResources && expansionTask is null)
            {
                return (startupCancellationTask, expansionCancellationTask, null, restartCancellationTask, restartTask);
            }

            var wasRunning = IsRunning;
            if (!startupInProgress || _captureLifecycle.HasActiveResources)
            {
                CleanupCapture_NoLock();
            }
            IsRunning = false;

            if (wasRunning)
            {
                Log.Information("[TextExpansionService] Stopped");
            }

            return (startupCancellationTask, expansionCancellationTask, expansionTask, restartCancellationTask, restartTask);
        }
    }

    private static async Task CompleteStopExpansionAsync(
        (Task? StartupCancellationTask, Task? ExpansionCancellationTask, Task? ExpansionTask, Task? RestartCancellationTask, Task? RestartTask) stop)
    {
        await AwaitCancellationAsync(
            stop.StartupCancellationTask,
            "[TextExpansionService] Error canceling startup").ConfigureAwait(false);
        await AwaitCancellationAsync(
            stop.ExpansionCancellationTask,
            "[TextExpansionService] Error canceling expansion").ConfigureAwait(false);
        await AwaitCancellationAsync(
            stop.RestartCancellationTask,
            "[TextExpansionService] Error canceling capture recovery").ConfigureAwait(false);
        if (stop.RestartTask is not null)
        {
            await stop.RestartTask.ConfigureAwait(false);
        }

        if (stop.ExpansionTask is not null)
        {
            await stop.ExpansionTask.ConfigureAwait(false);
        }
    }

    private static async Task AwaitCancellationAsync(Task? cancellationTask, string errorMessage)
    {
        if (cancellationTask is null)
        {
            return;
        }

        try
        {
            await cancellationTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, errorMessage);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        StopExpansion();
        _inputProcessor.CharacterReceived -= OnCharacterReceived;
        _inputProcessor.SpecialKeyReceived -= OnSpecialKeyReceived;
    }

    private void OnInputCaptureError(object? sender, InputCaptureErrorEventArgs e)
    {
        var error = e.Message;
        if (InputBackendErrorClassifier.IsKnownUnavailableMessage(error))
        {
            Log.Warning("[TextExpansionService] Input capture unavailable: {Error}", error);
        }
        else
        {
            Log.LogError("[TextExpansionService] Capture error: {Error}", error);
        }

        lock (_lock)
        {
            if (sender is not IInputCapture capture ||
                !IsRunning ||
                !_captureLifecycle.IsCurrent(capture) ||
                _restartCancellation is not null)
            {
                return;
            }
            // The recovery belongs to this capture lifetime, including its delayed continuation.
            var cancellation = new CancellationTokenSource();
            _restartCancellation = cancellation;
            RestartTask = TryRestartCaptureAsync(capture, _startupGeneration, cancellation, error);
        }
    }

    private async Task TryRestartCaptureAsync(IInputCapture capture, long generation, CancellationTokenSource cancellation, string cause)
    {
        try
        {
            await Task.Delay(TextExpansionExecutionTimings.CaptureRecoveryDelay, _timeProvider, cancellation.Token).ConfigureAwait(false);

            lock (_lock)
            {
                if (_disposed || !IsRunning || cancellation.IsCancellationRequested ||
                    generation != _startupGeneration || !_captureLifecycle.IsCurrent(capture))
                {
                    return;
                }

                Log.Warning("[TextExpansionService] Restarting input capture after error: {Cause}", cause);

                try
                {
                    CleanupCapture_NoLock();
                    IsRunning = false;
                    StartCaptureSession_NoLock();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    CleanupCapture_NoLock();
                    IsRunning = false;
                    Log.LogError(ex, "[TextExpansionService] Failed to restart input capture");
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Stopping the owning capture also cancels its delayed recovery.
        }
        finally
        {
            lock (_lock)
            {
                if (ReferenceEquals(_restartCancellation, cancellation))
                {
                    _restartCancellation = null;
                }
            }
            cancellation.Dispose();
        }
    }

    private void OnCaptureStarted(IInputCapture capture)
    {
        lock (_lock)
        {
            if (IsRunning && _captureLifecycle.IsCurrent(capture))
            {
                Log.Information("[TextExpansionService] Started via {Provider}", capture.ProviderName);
            }
        }
    }

    private void OnCaptureFaulted(IInputCapture capture, Exception ex)
    {
        Log.LogError(ex, "[TextExpansionService] Capture startup failed");

        lock (_lock)
        {
            if ((!IsRunning && !_asyncStartupInProgress) || !_captureLifecycle.IsCurrent(capture))
            {
                return;
            }

            CleanupCapture_NoLock();
            IsRunning = false;
        }
    }

    private void CleanupCapture_NoLock()
    {
        _captureLifecycle.Cleanup(
            OnInputReceived,
            OnInputCaptureError,
            ex => Log.LogError(ex, "[TextExpansionService] Error stopping"));
    }

    private void OnInputReceived(object? sender, CapturedInputEventArgs e)
    {
        lock (_lock)
        {
            if (!IsRunning)
            {
                return;
            }

            if (e.Type is InputEventType.Key && e.Value is 1)
            {
                _lastCharacterKeyCode = e.Code;
            }

            // Delegate to Processor
            _inputProcessor.ProcessEvent(e.Event);
        }
    }

    private void OnCharacterReceived(char c)
    {
        // Update Buffer
        _bufferState.Append(c);

        // Check for Trigger
        var expansions = _storageService.GetCurrent();
        if (_bufferState.TryGetMatch(expansions, out var match) && match is not null)
        {
            Log.Information(
                "[TextExpansionService] Trigger detected, scheduling expansion (triggerLength={TriggerLength}, replacementLength={ReplacementLength})",
                match.Trigger.Length,
                match.Replacement.Length);

            // Clear buffer immediately to prevent re-triggering
            _bufferState.Clear();

            var triggerLastKeyCode = _lastCharacterKeyCode;
            CancellationTokenSource expansionCancellation;
            lock (_lock)
            {
                if (!IsRunning || _expansionInProgress)
                {
                    return;
                }

                _expansionInProgress = true;
                expansionCancellation = new CancellationTokenSource();
                _expansionCancellation = expansionCancellation;
            }

            ExpansionTask = RunExpansionSafelyAsync(match, triggerLastKeyCode, expansionCancellation);
        }
    }

    private void OnSpecialKeyReceived(int keyCode)
    {
        if (keyCode == InputEventCode.KEY_BACKSPACE)
        {
            _bufferState.Backspace();
            Log.Debug("[TextExpansionService] Backspace received");
        }
        else if (keyCode == InputEventCode.KEY_ENTER)
        {
            _bufferState.Clear();
            Log.Debug("[TextExpansionService] Enter received, buffer cleared");
        }
    }

    private async Task PerformExpansionAsync(TextExpansionEntry expansion, int triggerLastKeyCode, CancellationToken cancellationToken)
    {
        // Ensure serialization of expansions
        await _expansionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Wait for Modifiers to be released (Safety)
            var modifierWaitStartedAt = _timeProvider.GetTimestamp();
            while (_inputProcessor.AreModifiersPressed)
            {
                var remaining = TextExpansionExecutionTimings.ModifierReleaseTimeout -
                    _timeProvider.GetElapsedTime(modifierWaitStartedAt);
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                await Task.Delay(GetPollDelay(remaining, TextExpansionExecutionTimings.ModifierReleasePollInterval), _timeProvider, cancellationToken).ConfigureAwait(false);
            }

            await WaitForTriggerKeyReleaseAsync(triggerLastKeyCode, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            Log.Debug(
                "[TextExpansionService] Executing expansion (triggerLength={TriggerLength}, replacementLength={ReplacementLength})",
                expansion.Trigger.Length,
                expansion.Replacement.Length);

            _inputProcessor.Suspend();
            try
            {
                await _startExecutor.ExpandAsync(expansion, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _inputProcessor.ResumeInputProcessing();
            }

            Log.Debug(
                "[TextExpansionService] Expansion completed (triggerLength={TriggerLength})",
                expansion.Trigger.Length);
        }
        finally
        {
            _ = _expansionLock.Release();
        }
    }

    private async Task WaitForTriggerKeyReleaseAsync(int keyCode, CancellationToken cancellationToken)
    {
        if (keyCode <= 0 || !_inputProcessor.IsKeyPressed(keyCode))
        {
            return;
        }

        var triggerReleaseWaitStartedAt = _timeProvider.GetTimestamp();
        while (_inputProcessor.IsKeyPressed(keyCode))
        {
            var remaining = TextExpansionExecutionTimings.TriggerKeyReleaseWaitTimeout -
                _timeProvider.GetElapsedTime(triggerReleaseWaitStartedAt);
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(GetPollDelay(remaining, TextExpansionExecutionTimings.DirectTypingInterElementDelay), _timeProvider, cancellationToken).ConfigureAwait(false);
        }

        if (_inputProcessor.IsKeyPressed(keyCode))
        {
            Log.Debug(
                "[TextExpansionService] Trigger key release wait timed out (keyCode={KeyCode}, timeoutMs={TimeoutMs})",
                keyCode,
                TextExpansionExecutionTimings.TriggerKeyReleaseWaitTimeout.TotalMilliseconds);
        }
    }

    private async Task RunExpansionSafelyAsync(TextExpansionEntry expansion, int triggerLastKeyCode, CancellationTokenSource expansionCancellation)
    {
        var cancellationToken = expansionCancellation.Token;
        try
        {
            await PerformExpansionAsync(expansion, triggerLastKeyCode, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Log.Debug("[TextExpansionService] Expansion canceled.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[TextExpansionService] Expansion failed");
        }
        finally
        {
            lock (_lock)
            {
                if (ReferenceEquals(_expansionCancellation, expansionCancellation))
                {
                    _expansionInProgress = false;
                    _expansionCancellation = null;
                    ExpansionTask = null;
                }
            }

            expansionCancellation.Dispose();
        }
    }

    private static TimeSpan GetPollDelay(TimeSpan remaining, TimeSpan pollInterval)
    {
        return remaining < pollInterval ? remaining : pollInterval;
    }
}
