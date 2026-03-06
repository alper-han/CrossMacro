using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class RecordExecutionService : IRecordExecutionService
{
    private static readonly TimeSpan StartupOutcomeSettleWindow = TimeSpan.FromMilliseconds(300);

    private enum StartupOutcomeState
    {
        Succeeded = 0,
        Canceled = 1,
        Faulted = 2
    }

    private readonly record struct StartupOutcome(StartupOutcomeState State, Exception? Error)
    {
        public static StartupOutcome Succeeded() => new(StartupOutcomeState.Succeeded, null);
        public static StartupOutcome Canceled(Exception? error = null) => new(StartupOutcomeState.Canceled, error);
        public static StartupOutcome Faulted(Exception? error = null) => new(StartupOutcomeState.Faulted, error);
    }

    private readonly IMacroRecorder _macroRecorder;
    private readonly IMacroFileManager _macroFileManager;
    private readonly IMousePositionProvider _mousePositionProvider;

    public RecordExecutionService(
        IMacroRecorder macroRecorder,
        IMacroFileManager macroFileManager,
        IMousePositionProvider mousePositionProvider)
    {
        _macroRecorder = macroRecorder;
        _macroFileManager = macroFileManager;
        _mousePositionProvider = mousePositionProvider;
    }

    public async Task<RecordExecutionResult> ExecuteAsync(RecordExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            return Fail(CliExitCode.InvalidArguments, "Output file path cannot be empty.");
        }

        if (!request.RecordMouse && !request.RecordKeyboard)
        {
            return Fail(CliExitCode.InvalidArguments, "At least one of mouse or keyboard recording must be enabled.");
        }

        if (request.DurationSeconds < 0)
        {
            return Fail(CliExitCode.InvalidArguments, "--duration must be >= 0.");
        }

        var warnings = new List<string>();
        var captureMode = await ResolveCaptureModeAsync(request.CoordinateMode, warnings, cancellationToken);
        var forceRelative = captureMode == RecordCoordinateMode.Relative;
        var skipInitialZero = forceRelative && request.SkipInitialZero;

        if (!forceRelative && request.SkipInitialZero)
        {
            warnings.Add("--skip-initial-zero only applies in relative mode; option ignored.");
        }

        MacroSequence? sequence = null;
        Exception? stopException = null;
        Exception? startupRuntimeException = null;
        var startupCompletedSuccessfully = false;
        var cancelledBeforeStart = false;
        using var recordingLifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Task startTask;
        try
        {
            startTask = _macroRecorder.StartRecordingAsync(
                request.RecordMouse,
                request.RecordKeyboard,
                ignoredKeys: null,
                forceRelative: forceRelative,
                skipInitialZero: skipInitialZero,
                cancellationToken: recordingLifetimeCts.Token);
        }
        catch (OperationCanceledException)
        {
            return Fail(CliExitCode.Cancelled, "Recording cancelled before start.");
        }
        catch (Exception ex)
        {
            return Fail(CliExitCode.EnvironmentError, "Failed to start recording.", [ex.Message], warnings);
        }

        var startupOutcomeTask = ObserveStartupOutcomeAsync(startTask);

        try
        {
            var startupOutcome = await WaitForStartupOutcomeAsync(startupOutcomeTask, cancellationToken);
            if (startupOutcome.State == StartupOutcomeState.Canceled)
            {
                cancelledBeforeStart = true;
            }
            else if (startupOutcome.State == StartupOutcomeState.Faulted)
            {
                startupRuntimeException = startupOutcome.Error ?? new InvalidOperationException("Unknown capture startup error.");
            }
            else
            {
                startupCompletedSuccessfully = true;
                if (request.DurationSeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(request.DurationSeconds), cancellationToken);
                }
                else
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!startupCompletedSuccessfully)
            {
                cancelledBeforeStart = true;
            }
        }
        finally
        {
            recordingLifetimeCts.Cancel();

            if (_macroRecorder.IsRecording)
            {
                try
                {
                    sequence = _macroRecorder.StopRecording();
                }
                catch (InvalidOperationException) when (!_macroRecorder.IsRecording)
                {
                    // Recorder can transition to stopped between check and stop call during cancellation.
                }
                catch (Exception ex)
                {
                    stopException = ex;
                }
            }
        }

        if (!startupCompletedSuccessfully)
        {
            var settledStartupOutcome = await TryGetStartupOutcomeAsync(startupOutcomeTask, StartupOutcomeSettleWindow);
            if (settledStartupOutcome.HasValue)
            {
                var outcome = settledStartupOutcome.Value;
                if (outcome.State == StartupOutcomeState.Faulted)
                {
                    startupRuntimeException ??= outcome.Error ?? new InvalidOperationException("Unknown capture startup error.");
                }
                else if (outcome.State == StartupOutcomeState.Canceled)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        cancelledBeforeStart = true;
                    }
                    else
                    {
                        startupRuntimeException ??= outcome.Error ?? new OperationCanceledException("Capture startup was cancelled.");
                    }
                }
                else
                {
                    startupCompletedSuccessfully = true;
                }
            }
            else
            {
                ObserveFaultedTask(startTask);
            }
        }

        var hasRecordedEvents = sequence is { Events.Count: > 0 };

        if (startupRuntimeException != null)
        {
            return Fail(CliExitCode.EnvironmentError, "Failed to start recording.", [startupRuntimeException.Message], warnings);
        }

        if (stopException != null)
        {
            return Fail(CliExitCode.RuntimeError, "Recording failed while stopping.", [stopException.Message], warnings);
        }

        if (hasRecordedEvents)
        {
            try
            {
                sequence!.Name = Path.GetFileNameWithoutExtension(request.OutputFilePath);
                await _macroFileManager.SaveAsync(sequence, request.OutputFilePath);
            }
            catch (Exception ex)
            {
                return Fail(CliExitCode.FileError, "Failed to save recorded macro.", [ex.Message], warnings);
            }

            var data = new
            {
                outputPath = request.OutputFilePath,
                eventCount = sequence.Events.Count,
                totalDurationMs = sequence.TotalDurationMs,
                recordMouse = request.RecordMouse,
                recordKeyboard = request.RecordKeyboard,
                requestedMode = request.CoordinateMode.ToString().ToLowerInvariant(),
                actualMode = captureMode.ToString().ToLowerInvariant(),
                skipInitialZero = skipInitialZero
            };

            return new RecordExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Recording completed.",
                Warnings = warnings,
                Data = data
            };
        }

        if (cancelledBeforeStart)
        {
            return Fail(CliExitCode.Cancelled, "Recording cancelled before start.");
        }

        if (sequence == null)
        {
            return Fail(CliExitCode.RuntimeError, "Recording did not produce a macro.");
        }

        if (sequence.Events.Count == 0)
        {
            return Fail(CliExitCode.RuntimeError, "No events were recorded.", warnings: warnings);
        }

        throw new InvalidOperationException("Unexpected recording result state.");
    }

    private static async Task<StartupOutcome> WaitForStartupOutcomeAsync(
        Task<StartupOutcome> startupOutcomeTask,
        CancellationToken cancellationToken)
    {
        if (startupOutcomeTask.IsCompleted)
        {
            return await startupOutcomeTask;
        }

        var completedTask = await Task.WhenAny(startupOutcomeTask, Task.Delay(Timeout.Infinite, cancellationToken));

        if (ReferenceEquals(completedTask, startupOutcomeTask))
        {
            return await startupOutcomeTask;
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

    private static async Task<StartupOutcome?> TryGetStartupOutcomeAsync(
        Task<StartupOutcome> startupOutcomeTask,
        TimeSpan timeout)
    {
        if (startupOutcomeTask.IsCompleted)
        {
            return await startupOutcomeTask;
        }

        var completedTask = await Task.WhenAny(startupOutcomeTask, Task.Delay(timeout));
        if (!ReferenceEquals(completedTask, startupOutcomeTask))
        {
            return null;
        }

        return await startupOutcomeTask;
    }

    private async Task<RecordCoordinateMode> ResolveCaptureModeAsync(
        RecordCoordinateMode requestedMode,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (requestedMode == RecordCoordinateMode.Relative)
        {
            return RecordCoordinateMode.Relative;
        }

        if (requestedMode == RecordCoordinateMode.Absolute)
        {
            var absoluteSupported = await CanUseAbsoluteModeAsync(cancellationToken);
            if (absoluteSupported)
            {
                return RecordCoordinateMode.Absolute;
            }

            warnings.Add("Absolute mode is not supported in this environment. Falling back to relative mode.");
            return RecordCoordinateMode.Relative;
        }

        // Auto: prefer absolute when available.
        return await CanUseAbsoluteModeAsync(cancellationToken)
            ? RecordCoordinateMode.Absolute
            : RecordCoordinateMode.Relative;
    }

    private async Task<bool> CanUseAbsoluteModeAsync(CancellationToken cancellationToken)
    {
        if (!_mousePositionProvider.IsSupported)
        {
            return false;
        }

        try
        {
            var position = await _mousePositionProvider.GetAbsolutePositionAsync();
            return position.HasValue;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static RecordExecutionResult Fail(
        CliExitCode exitCode,
        string message,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null)
    {
        return new RecordExecutionResult
        {
            Success = false,
            ExitCode = exitCode,
            Message = message,
            Errors = errors ?? [],
            Warnings = warnings ?? []
        };
    }
}
