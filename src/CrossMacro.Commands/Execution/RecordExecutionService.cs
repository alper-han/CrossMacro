using CrossMacro.Core.Logging;
using CrossMacro.Core.Models.Macros;
using CrossMacro.Core.Services.Macros;
using CrossMacro.Core.Services.Recording;

namespace CrossMacro.Commands.Execution;

public sealed class RecordExecutionService(
    IMacroRecorder macroRecorder,
    IMacroFileManager macroFileManager,
    IMousePositionProvider mousePositionProvider,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null) : IRecordExecutionService
{
    private static readonly TimeSpan StartupOutcomeSettleWindow = TimeSpan.FromMilliseconds(300);
    private static readonly Func<TimeSpan, CancellationToken, Task> DefaultDelayAsync = Task.Delay;

    private enum StartupOutcomeState
    {
        Succeeded = 0,
        Canceled = 1,
        Faulted = 2,
    }

    private readonly record struct StartupOutcome(StartupOutcomeState State, Exception? Error)
    {
        public static StartupOutcome Succeeded() => new(StartupOutcomeState.Succeeded, Error: null);
        public static StartupOutcome Canceled(Exception? error = null) => new(StartupOutcomeState.Canceled, error);
        public static StartupOutcome Faulted(Exception? error = null) => new(StartupOutcomeState.Faulted, error);
    }

    private readonly IMacroRecorder _macroRecorder = macroRecorder;
    private readonly IMacroFileManager _macroFileManager = macroFileManager;
    private readonly IMousePositionProvider _mousePositionProvider = mousePositionProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync = delayAsync ?? DefaultDelayAsync;

    private sealed class CaptureOutcome
    {
        public MacroSequence? Sequence { get; set; }

        public Exception? StopException { get; set; }

        public Exception? StartupException { get; set; }

        public bool StartupCompleted { get; set; }

        public bool CancelledBeforeStart { get; set; }
    }

    public async Task<OperationResult> ExecuteAsync(RecordExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        var warnings = new List<string>();
        var captureMode = await ResolveCaptureModeAsync(request.CoordinateMode, warnings).ConfigureAwait(false);
        var forceRelative = captureMode is RecordCoordinateMode.Relative;
        var skipInitialZero = forceRelative && request.SkipInitialZero;

        if (!forceRelative && request.SkipInitialZero)
        {
            warnings.Add("--skip-initial-zero only applies in relative mode; option ignored.");
        }

        var recordingLifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!TryStartRecorder(request, forceRelative, skipInitialZero, warnings, recordingLifetimeCts.Token, out var startTask, out var startFailure))
        {
            recordingLifetimeCts.Dispose();
            return startFailure;
        }

        var startupOutcomeTask = ObserveStartupOutcomeAsync(startTask);
        try
        {
            var capture = await CaptureAsync(request.DurationSeconds, startupOutcomeTask, recordingLifetimeCts, cancellationToken).ConfigureAwait(false);
            await SettleStartupAsync(capture, startTask, startupOutcomeTask, cancellationToken).ConfigureAwait(false);
            return await BuildResultAsync(request, capture, warnings).ConfigureAwait(false);
        }
        finally
        {
            DisposeRecordingLifetimeCts(startTask, recordingLifetimeCts);
        }
    }

    private static OperationResult? Validate(RecordExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Output file path cannot be empty.");
        }

        if (!request.RecordMouse && !request.RecordKeyboard)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "At least one of mouse or keyboard recording must be enabled.");
        }

        return request.DurationSeconds < 0
            ? OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "--duration must be >= 0.")
            : null;
    }

    private bool TryStartRecorder(
        RecordExecutionRequest request,
        bool forceRelative,
        bool skipInitialZero,
        List<string> warnings,
        CancellationToken recordingToken,
        [NotNullWhen(true)] out Task? startTask,
        [NotNullWhen(false)] out OperationResult? failure)
    {
        try
        {
            startTask = _macroRecorder.StartRecordingAsync(
                request.RecordMouse,
                request.RecordKeyboard,
                ignoredKeys: null,
                forceRelative: forceRelative,
                skipInitialZero: skipInitialZero,
                cancellationToken: recordingToken);
            failure = null;
            return true;
        }
        catch (OperationCanceledException)
        {
            startTask = null;
            failure = OperationResult.Fail(ExecutionOutcomeCode.Cancelled, "Recording cancelled before start.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            startTask = null;
            failure = OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, "Failed to start recording.", [ex.Message], warnings);
        }

        return false;
    }

    /// <summary>Waits for the recorder to start, records for the requested duration or until cancelled, then stops it.</summary>
    private async Task<CaptureOutcome> CaptureAsync(
        int durationSeconds,
        Task<StartupOutcome> startupOutcomeTask,
        CancellationTokenSource recordingLifetimeCts,
        CancellationToken cancellationToken)
    {
        var capture = new CaptureOutcome();
        try
        {
            var startupOutcome = await WaitForStartupOutcomeAsync(startupOutcomeTask, cancellationToken).ConfigureAwait(false);
            if (startupOutcome.State is StartupOutcomeState.Canceled)
            {
                capture.CancelledBeforeStart = true;
            }
            else if (startupOutcome.State is StartupOutcomeState.Faulted)
            {
                capture.StartupException = startupOutcome.Error ?? new InvalidOperationException("Unknown capture startup error.");
            }
            else
            {
                capture.StartupCompleted = true;
                await _delayAsync(
                    durationSeconds > 0 ? TimeSpan.FromSeconds(durationSeconds) : Timeout.InfiniteTimeSpan,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!capture.StartupCompleted)
            {
                capture.CancelledBeforeStart = true;
            }
        }
        finally
        {
            await recordingLifetimeCts.CancelAsync().ConfigureAwait(false);
            StopRecorder(capture);
        }

        return capture;
    }

    private void StopRecorder(CaptureOutcome capture)
    {
        if (!_macroRecorder.IsRecording)
        {
            return;
        }

        try
        {
            capture.Sequence = _macroRecorder.StopRecording();
        }
        catch (InvalidOperationException) when (!_macroRecorder.IsRecording)
        {
            // Recorder can transition to stopped between check and stop call during cancellation.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            capture.StopException = ex;
        }
    }

    /// <summary>Classifies a startup that had not completed when capture ended, once it settles or the settle window elapses.</summary>
    private async Task SettleStartupAsync(
        CaptureOutcome capture,
        Task startTask,
        Task<StartupOutcome> startupOutcomeTask,
        CancellationToken cancellationToken)
    {
        if (capture.StartupCompleted)
        {
            return;
        }

        var settledStartupOutcome = await TryGetStartupOutcomeAsync(startupOutcomeTask, StartupOutcomeSettleWindow).ConfigureAwait(false);
        if (settledStartupOutcome is not { } outcome)
        {
            ObserveFaultedTask(startTask);
            return;
        }

        if (outcome.State is StartupOutcomeState.Faulted)
        {
            capture.StartupException ??= outcome.Error ?? new InvalidOperationException("Unknown capture startup error.");
        }
        else if (outcome.State is StartupOutcomeState.Canceled)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                capture.CancelledBeforeStart = true;
            }
            else
            {
                capture.StartupException ??= outcome.Error ?? new OperationCanceledException("Capture startup was cancelled.");
            }
        }
    }

    private async Task<OperationResult> BuildResultAsync(RecordExecutionRequest request, CaptureOutcome capture, List<string> warnings)
    {
        if (capture.StartupException is not null)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, "Failed to start recording.", [capture.StartupException.Message], warnings);
        }

        if (capture.StopException is not null)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "Recording failed while stopping.", [capture.StopException.Message], warnings);
        }

        if (capture.Sequence is { Events.Count: > 0 } recordedSequence)
        {
            return await SaveAsync(request, recordedSequence, warnings).ConfigureAwait(false);
        }

        if (capture.CancelledBeforeStart)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.Cancelled, "Recording cancelled before start.");
        }

        return capture.Sequence is null
            ? OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "Recording did not produce a macro.")
            : OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "No events were recorded.", warnings: warnings);
    }

    private async Task<OperationResult> SaveAsync(RecordExecutionRequest request, MacroSequence recordedSequence, List<string> warnings)
    {
        try
        {
            recordedSequence.Name = Path.GetFileNameWithoutExtension(request.OutputFilePath);
            await _macroFileManager.SaveAsync(recordedSequence, request.OutputFilePath).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return OperationResult.Fail(ExecutionOutcomeCode.FileError, "Failed to save recorded macro.", [ex.Message], warnings);
        }

        var data = new RecordExecutionData(
            request.OutputFilePath,
            recordedSequence.Events.Count,
            recordedSequence.TotalDurationMs,
            request.RecordMouse,
            request.RecordKeyboard,
            request.CoordinateMode.ToString().ToLowerInvariant(),
            recordedSequence.IsAbsoluteCoordinates ? "absolute" : "relative",
            recordedSequence.SkipInitialZeroZero);

        return OperationResult.Ok("Recording completed.", data, warnings);
    }

    private async Task<StartupOutcome> WaitForStartupOutcomeAsync(
        Task<StartupOutcome> startupOutcomeTask,
        CancellationToken cancellationToken)
    {
        if (startupOutcomeTask.IsCompleted)
        {
            return await startupOutcomeTask.ConfigureAwait(false);
        }

        var completedTask = await Task.WhenAny(startupOutcomeTask, _delayAsync(Timeout.InfiniteTimeSpan, cancellationToken)).ConfigureAwait(false);

        if (ReferenceEquals(completedTask, startupOutcomeTask))
        {
            return await startupOutcomeTask.ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return StartupOutcome.Canceled(new OperationCanceledException("Recording cancelled before start.", cancellationToken));
        }

        return StartupOutcome.Canceled();
    }

    private static Task<StartupOutcome> ObserveStartupOutcomeAsync(Task startTask)
    {
        return startTask.ContinueWith(
            static task =>
            {
                if (task.IsFaulted)
                {
                    return StartupOutcome.Faulted(task.Exception?.GetBaseException() ?? new InvalidOperationException("Unknown capture startup error."));
                }

                if (task.IsCanceled)
                {
                    return StartupOutcome.Canceled(new OperationCanceledException("Capture startup was cancelled."));
                }

                return StartupOutcome.Succeeded();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void ObserveFaultedTask(Task task)
    {
        _ = task.ContinueWith(
            static faultedTask => _ = faultedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void DisposeRecordingLifetimeCts(Task startTask, CancellationTokenSource recordingLifetimeCts)
    {
        if (startTask.IsCompleted)
        {
            recordingLifetimeCts.Dispose();
            return;
        }

        _ = startTask.ContinueWith(
            static (_, state) => ((CancellationTokenSource)state!).Dispose(),
            recordingLifetimeCts,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task<StartupOutcome?> TryGetStartupOutcomeAsync(
        Task<StartupOutcome> startupOutcomeTask,
        TimeSpan timeout)
    {
        if (startupOutcomeTask.IsCompleted)
        {
            return await startupOutcomeTask.ConfigureAwait(false);
        }

        using var timeoutCancellation = new CancellationTokenSource();
        var timeoutTask = _delayAsync(timeout, timeoutCancellation.Token);
        var completedTask = await Task.WhenAny(startupOutcomeTask, timeoutTask).ConfigureAwait(false);
        if (!ReferenceEquals(completedTask, startupOutcomeTask))
        {
            return null;
        }

        await timeoutCancellation.CancelAsync().ConfigureAwait(false);

        try
        {
            await timeoutTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The settle timer was cancelled because the startup outcome arrived first.
        }

        return await startupOutcomeTask.ConfigureAwait(false);
    }

    private async Task<RecordCoordinateMode> ResolveCaptureModeAsync(
        RecordCoordinateMode requestedMode,
        List<string> warnings)
    {
        if (requestedMode is RecordCoordinateMode.Relative)
        {
            return RecordCoordinateMode.Relative;
        }

        if (requestedMode is RecordCoordinateMode.Absolute)
        {
            var absoluteSupported = await CanUseAbsoluteModeAsync().ConfigureAwait(false);
            if (absoluteSupported)
            {
                return RecordCoordinateMode.Absolute;
            }

            warnings.Add("Absolute mode is not supported in this environment. Falling back to relative mode.");
            return RecordCoordinateMode.Relative;
        }

        // Auto: prefer absolute when available.
        return await CanUseAbsoluteModeAsync().ConfigureAwait(false)
            ? RecordCoordinateMode.Absolute
            : RecordCoordinateMode.Relative;
    }

    private async Task<bool> CanUseAbsoluteModeAsync()
    {
        if (!_mousePositionProvider.IsSupported)
        {
            return false;
        }

        try
        {
            var position = await _mousePositionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
            return position is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            Log.Debug(ex, "[RecordExecutionService] Absolute position probe failed; falling back to relative mode");
            return false;
        }
    }
}
