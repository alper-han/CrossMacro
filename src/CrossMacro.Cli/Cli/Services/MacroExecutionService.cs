using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

public sealed class MacroExecutionService : IMacroExecutionService
{
    private readonly IMacroFileManager _macroFileManager;
    private readonly Func<IMacroPlayer> _macroPlayerFactory;
    private static readonly PlaybackValidator Validator = new(new NullMousePositionProvider("CLI Validation Provider"));

    public MacroExecutionService(IMacroFileManager macroFileManager, Func<IMacroPlayer> macroPlayerFactory)
    {
        _macroFileManager = macroFileManager;
        _macroPlayerFactory = macroPlayerFactory;
    }

    public Task<MacroExecutionResult> ValidateAsync(string macroFilePath, CancellationToken cancellationToken)
    {
        return ExecuteCoreAsync(macroFilePath, dryRun: true, options: null, countdownSeconds: 0, cancellationToken);
    }

    public async Task<MacroExecutionResult> GetInfoAsync(string macroFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(macroFilePath) || !File.Exists(macroFilePath))
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.FileError,
                Message = "Macro file not found.",
                Errors = [$"File does not exist: {macroFilePath}"]
            };
        }

        MacroSequence? macro;
        try
        {
            macro = await _macroFileManager.LoadAsync(macroFilePath);
        }
        catch (Exception ex)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.FileError,
                Message = "Failed to read macro file.",
                Errors = [ex.Message]
            };
        }

        if (macro == null)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.ValidationError,
                Message = "Macro file could not be loaded."
            };
        }

        var validation = Validator.Validate(macro);
        var data = BuildInfoData(macroFilePath, macro);
        var message = validation.IsValid
            ? "Macro info loaded."
            : "Macro info loaded with validation errors.";

        if (!validation.IsValid)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.ValidationError,
                Message = message,
                Errors = validation.Errors,
                Warnings = validation.Warnings,
                Data = data
            };
        }

        return new MacroExecutionResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Message = message,
            Warnings = validation.Warnings,
            Data = data
        };
    }

    public Task<MacroExecutionResult> ExecuteAsync(MacroExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var playbackOptions = new PlaybackOptions
        {
            SpeedMultiplier = request.SpeedMultiplier,
            Loop = request.Loop,
            RepeatCount = request.RepeatCount,
            RepeatDelayMs = request.RepeatDelayMs
        };

        return ExecuteCoreAsync(
            request.MacroFilePath,
            request.DryRun,
            playbackOptions,
            request.CountdownSeconds,
            cancellationToken);
    }

    private async Task<MacroExecutionResult> ExecuteCoreAsync(
        string macroFilePath,
        bool dryRun,
        PlaybackOptions? options,
        int countdownSeconds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(macroFilePath) || !File.Exists(macroFilePath))
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.FileError,
                Message = "Macro file not found.",
                Errors = [$"File does not exist: {macroFilePath}"]
            };
        }

        MacroSequence? macro;
        try
        {
            macro = await _macroFileManager.LoadAsync(macroFilePath);
        }
        catch (Exception ex)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.FileError,
                Message = "Failed to read macro file.",
                Errors = [ex.Message]
            };
        }

        if (macro == null)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.ValidationError,
                Message = "Macro file could not be loaded."
            };
        }

        var validation = Validator.Validate(macro);
        if (!validation.IsValid)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.ValidationError,
                Message = "Macro validation failed.",
                Errors = validation.Errors,
                Warnings = validation.Warnings,
                Data = new
                {
                    macroPath = macroFilePath,
                    eventCount = macro.EventCount
                }
            };
        }

        if (dryRun)
        {
            return new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Macro is valid.",
                Warnings = validation.Warnings,
                Data = BuildSummaryData(macroFilePath, macro)
            };
        }

        try
        {
            if (countdownSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(countdownSeconds), cancellationToken);
            }

            using var player = _macroPlayerFactory();
            using var stopRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    player.Stop();
                }
                catch
                {
                }
            });

            await player.PlayAsync(macro, options, cancellationToken);

            return new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Playback complete.",
                Warnings = validation.Warnings,
                Data = BuildSummaryData(macroFilePath, macro)
            };
        }
        catch (OperationCanceledException)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.Cancelled,
                Message = "Playback cancelled."
            };
        }
        catch (Exception ex)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.RuntimeError,
                Message = "Playback failed.",
                Errors = [ex.Message],
                Warnings = validation.Warnings,
                Data = BuildSummaryData(macroFilePath, macro)
            };
        }
    }

    private static object BuildSummaryData(string macroFilePath, MacroSequence macro)
    {
        return new
        {
            macroPath = macroFilePath,
            macroName = macro.Name,
            eventCount = macro.EventCount,
            totalDurationMs = macro.TotalDurationMs,
            isAbsoluteCoordinates = macro.IsAbsoluteCoordinates
        };
    }

    private static object BuildInfoData(string macroFilePath, MacroSequence macro)
    {
        var totalDuration = macro.TotalDurationMs;
        if (totalDuration <= 0)
        {
            macro.CalculateDuration();
            totalDuration = macro.TotalDurationMs;
        }

        return new
        {
            macroPath = macroFilePath,
            macroName = macro.Name,
            createdAt = macro.CreatedAt,
            eventCount = macro.EventCount,
            totalDurationMs = totalDuration,
            isAbsoluteCoordinates = macro.IsAbsoluteCoordinates,
            skipInitialZeroZero = macro.SkipInitialZeroZero,
            trailingDelayMs = macro.TrailingDelayMs,
            hasTrailingRandomDelay = macro.HasTrailingRandomDelay,
            trailingDelayMinMs = macro.TrailingDelayMinMs,
            trailingDelayMaxMs = macro.TrailingDelayMaxMs,
            eventBreakdown = new
            {
                mouseMove = macro.Events.Count(e => e.Type == EventType.MouseMove),
                buttonPress = macro.Events.Count(e => e.Type == EventType.ButtonPress),
                buttonRelease = macro.Events.Count(e => e.Type == EventType.ButtonRelease),
                click = macro.Events.Count(e => e.Type == EventType.Click),
                keyPress = macro.Events.Count(e => e.Type == EventType.KeyPress),
                keyRelease = macro.Events.Count(e => e.Type == EventType.KeyRelease)
            }
        };
    }

}
