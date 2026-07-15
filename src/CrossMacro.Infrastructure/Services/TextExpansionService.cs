
namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for monitoring keystrokes and performing text expansion.
/// Refactored to coordinate InputProcessor, BufferState, and Executor.
/// </summary>
public class TextExpansionService : ITextExpansionService
{
    private readonly ISettingsService _settingsService;
    private readonly ITextExpansionStorageService _storageService;
    private readonly Func<IInputCapture> _inputCaptureFactory;

    // Decomposed Components
    private readonly IInputProcessor _inputProcessor;
    private readonly ITextBufferState _bufferState;
    private readonly ITextExpansionExecutor _startExecutor;

    // Lifecycle management
    private readonly Lock _lock;
    private bool _isRunning;
    private readonly SemaphoreSlim _expansionLock;
    private bool _expansionInProgress;
    private CancellationTokenSource? _expansionCancellation;
    private bool _disposed;
    private readonly InputCaptureLifecycle _captureLifecycle;
    private int _lastCharacterKeyCode;

    public bool IsRunning => _isRunning;

    public TextExpansionService(
        ISettingsService settingsService,
        ITextExpansionStorageService storageService,
        Func<IInputCapture> inputCaptureFactory,
        IInputProcessor inputProcessor,
        ITextBufferState bufferState,
        ITextExpansionExecutor startExecutor)
    {
        _settingsService = settingsService;
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
            if (_disposed || _isRunning)
            {
                return;
            }

            try
            {
                _storageService.Load();
                _isRunning = true;
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
            catch (Exception ex)
            {
                Log.LogError(ex, "[TextExpansionService] Failed to start");
                CleanupCapture_NoLock();
                _isRunning = false;
            }
        }
    }

    public void StopExpansion()
    {
        lock (_lock)
        {
            _expansionCancellation?.Cancel();

            if (!_isRunning && !_captureLifecycle.HasActiveResources)
            {
                return;
            }

            var wasRunning = _isRunning;
            CleanupCapture_NoLock();
            _isRunning = false;

            if (wasRunning)
            {
                Log.Information("[TextExpansionService] Stopped");
            }
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
        Log.LogError("[TextExpansionService] Capture error: {Error}", error);

        lock (_lock)
        {
            if (sender is not IInputCapture capture ||
                !_isRunning ||
                !_captureLifecycle.IsCurrent(capture))
            {
                return;
            }

            CleanupCapture_NoLock();
            _isRunning = false;
            Log.Information("[TextExpansionService] Stopped");
        }
    }

    private void OnCaptureStarted(IInputCapture capture)
    {
        lock (_lock)
        {
            if (_isRunning && _captureLifecycle.IsCurrent(capture))
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
            if (!_isRunning || !_captureLifecycle.IsCurrent(capture))
            {
                return;
            }

            CleanupCapture_NoLock();
            _isRunning = false;
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
            if (!_isRunning)
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
                if (!_isRunning || _expansionInProgress)
                {
                    return;
                }

                _expansionInProgress = true;
                expansionCancellation = new CancellationTokenSource();
                _expansionCancellation = expansionCancellation;
            }

            _ = Task.Run(() => RunExpansionSafelyAsync(match, triggerLastKeyCode, expansionCancellation));
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

    private async Task PerformExpansionAsync(Core.Models.TextExpansionEntry expansion, int triggerLastKeyCode, CancellationToken cancellationToken)
    {
        // Ensure serialization of expansions
        await _expansionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Wait for Modifiers to be released (Safety)
            var modifierWaitStartedAt = Stopwatch.GetTimestamp();
            while (_inputProcessor.AreModifiersPressed)
            {
                var remaining = TextExpansionExecutionTimings.ModifierReleaseTimeout -
                    Stopwatch.GetElapsedTime(modifierWaitStartedAt);
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                await Task.Delay(GetPollDelay(remaining, TextExpansionExecutionTimings.ModifierReleasePollInterval), cancellationToken).ConfigureAwait(false);
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
                await _startExecutor.ExpandAsync(expansion).ConfigureAwait(false);
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
            _expansionLock.Release();
        }
    }

    private async Task WaitForTriggerKeyReleaseAsync(int keyCode, CancellationToken cancellationToken)
    {
        if (keyCode <= 0 || !_inputProcessor.IsKeyPressed(keyCode))
        {
            return;
        }

        var triggerReleaseWaitStartedAt = Stopwatch.GetTimestamp();
        while (_inputProcessor.IsKeyPressed(keyCode))
        {
            var remaining = TextExpansionExecutionTimings.TriggerKeyReleaseWaitTimeout -
                Stopwatch.GetElapsedTime(triggerReleaseWaitStartedAt);
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(GetPollDelay(remaining, TextExpansionExecutionTimings.DirectTypingInterElementDelay), cancellationToken).ConfigureAwait(false);
        }

        if (_inputProcessor.IsKeyPressed(keyCode))
        {
            Log.Debug(
                "[TextExpansionService] Trigger key release wait timed out (keyCode={KeyCode}, timeoutMs={TimeoutMs})",
                keyCode,
                TextExpansionExecutionTimings.TriggerKeyReleaseWaitTimeout.TotalMilliseconds);
        }
    }

    private async Task RunExpansionSafelyAsync(Core.Models.TextExpansionEntry expansion, int triggerLastKeyCode, CancellationTokenSource expansionCancellation)
    {
        var cancellationToken = expansionCancellation.Token;
        try
        {
            await PerformExpansionAsync(expansion, triggerLastKeyCode, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
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
