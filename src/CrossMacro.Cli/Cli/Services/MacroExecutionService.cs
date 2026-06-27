using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Playback;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Cli.Serialization;

namespace CrossMacro.Cli.Services;

public sealed class MacroExecutionService : IMacroExecutionService
{
    private readonly IMacroFileManager _macroFileManager;
    private readonly Func<IMacroPlayer> _macroPlayerFactory;
    private readonly PlaybackValidator _validator;

    public MacroExecutionService(
        IMacroFileManager macroFileManager,
        Func<IMacroPlayer> macroPlayerFactory,
        IKeyCodeMapper keyCodeMapper)
    {
        _macroFileManager = macroFileManager ?? throw new ArgumentNullException(nameof(macroFileManager));
        _macroPlayerFactory = macroPlayerFactory ?? throw new ArgumentNullException(nameof(macroPlayerFactory));
        _validator = new PlaybackValidator(
            keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper)),
            new NullMousePositionProvider("CLI Validation Provider"));
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

        var validation = _validator.Validate(macro);
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

        var validation = _validator.Validate(macro);
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
            if (ex is AbsolutePlaybackUnsupportedException)
            {
                return new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = CliExitCode.RuntimeError,
                    Message = "Absolute coordinate playback is not supported in this session.",
                    Errors = ["This macro contains absolute mouse coordinates, but the active backend cannot play absolute coordinates. Use a backend/session with absolute coordinate support or edit the macro to use relative coordinates."],
                    Warnings = validation.Warnings,
                    Data = BuildSummaryData(macroFilePath, macro)
                };
            }

            if (ex is InputInjectionPermissionRequiredException)
            {
                return new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = CliExitCode.EnvironmentError,
                    Message = "Playback permission is missing.",
                    Errors = [ex.Message],
                    Warnings = validation.Warnings,
                    Data = BuildSummaryData(macroFilePath, macro)
                };
            }

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

    private static MacroSummaryData BuildSummaryData(string macroFilePath, MacroSequence macro)
    {
        var coordinateMode = MacroPositionSemantics.GetCoordinateModeSummary(macro) switch
        {
            CoordinateModeSummary.Absolute => "absolute",
            CoordinateModeSummary.Relative => "relative",
            CoordinateModeSummary.Mixed => "mixed",
            _ => "none"
        };

        return new MacroSummaryData(
            macroFilePath,
            macro.Name,
            macro.EventCount,
            macro.TotalDurationMs,
            coordinateMode,
            macro.IsAbsoluteCoordinates
        );
    }

    private static MacroInfoData BuildInfoData(string macroFilePath, MacroSequence macro)
    {
        var totalDuration = macro.TotalDurationMs;
        if (totalDuration <= 0)
        {
            macro.CalculateDuration();
            totalDuration = macro.TotalDurationMs;
        }

        var coordinateMode = MacroPositionSemantics.GetCoordinateModeSummary(macro) switch
        {
            CoordinateModeSummary.Absolute => "absolute",
            CoordinateModeSummary.Relative => "relative",
            CoordinateModeSummary.Mixed => "mixed",
            _ => "none"
        };

        var eventBreakdown = new MacroEventBreakdownData(
            macro.Events.Count(e => e.Type == EventType.MouseMove),
            macro.Events.Count(e => e.Type == EventType.ButtonPress),
            macro.Events.Count(e => e.Type == EventType.ButtonRelease),
            macro.Events.Count(e => e.Type == EventType.Click),
            macro.Events.Count(e => e.Type == EventType.KeyPress),
            macro.Events.Count(e => e.Type == EventType.KeyRelease)
        );

        return new MacroInfoData(
            macroFilePath,
            macro.Name,
            macro.CreatedAt,
            macro.EventCount,
            totalDuration,
            coordinateMode,
            macro.IsAbsoluteCoordinates,
            macro.SkipInitialZeroZero,
            macro.TrailingDelayMs,
            macro.HasTrailingRandomDelay,
            macro.TrailingDelayMinMs,
            macro.TrailingDelayMaxMs,
            eventBreakdown
        );
    }

}
