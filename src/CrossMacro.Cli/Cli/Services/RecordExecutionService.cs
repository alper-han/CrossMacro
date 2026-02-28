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

        try
        {
            await _macroRecorder.StartRecordingAsync(
                request.RecordMouse,
                request.RecordKeyboard,
                ignoredKeys: null,
                forceRelative: forceRelative,
                skipInitialZero: skipInitialZero,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Fail(CliExitCode.Cancelled, "Recording cancelled before start.");
        }
        catch (Exception ex)
        {
            return Fail(CliExitCode.EnvironmentError, "Failed to start recording.", [ex.Message], warnings);
        }

        try
        {
            if (request.DurationSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(request.DurationSeconds), cancellationToken);
            }
            else
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C is the expected stop trigger for record command.
        }
        finally
        {
            if (_macroRecorder.IsRecording)
            {
                try
                {
                    sequence = _macroRecorder.StopRecording();
                }
                catch (Exception ex)
                {
                    stopException = ex;
                }
            }
        }

        if (stopException != null)
        {
            return Fail(CliExitCode.RuntimeError, "Recording failed while stopping.", [stopException.Message], warnings);
        }

        if (sequence == null)
        {
            return Fail(CliExitCode.RuntimeError, "Recording did not produce a macro.");
        }

        if (sequence.Events.Count == 0)
        {
            return Fail(CliExitCode.RuntimeError, "No events were recorded.", warnings: warnings);
        }

        try
        {
            sequence.Name = Path.GetFileNameWithoutExtension(request.OutputFilePath);
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
