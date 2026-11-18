
namespace CrossMacro.Cli.Services.Execution;

public sealed class MacroExecutionService(
    IMacroFileManager macroFileManager,
    Func<IMacroPlayer> macroPlayerFactory,
    IPlaybackValidator validator,
    TimeProvider? timeProvider = null) : IMacroExecutionService
{
    private readonly IMacroFileManager _macroFileManager = macroFileManager ?? throw new ArgumentNullException(nameof(macroFileManager));
    private readonly Func<IMacroPlayer> _macroPlayerFactory = macroPlayerFactory ?? throw new ArgumentNullException(nameof(macroPlayerFactory));
    private readonly IPlaybackValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Task<MacroExecutionResult> ValidateAsync(string macroFilePath, CancellationToken cancellationToken)
    {
        return ExecuteCoreAsync(macroFilePath, dryRun: true, options: null, countdownSeconds: 0, cancellationToken);
    }

    public async Task<MacroExecutionResult> GetInfoAsync(string macroFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var load = await LoadMacroAsync(macroFilePath).ConfigureAwait(false);
        if (load.Failure is { } loadFailure)
        {
            return loadFailure;
        }

        var macro = load.Macro!;

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
                ExitCode = ExecutionOutcomeCode.ValidationError,
                Message = message,
                Errors = validation.Errors,
                Warnings = validation.Warnings,
                Data = data,
            };
        }

        return new MacroExecutionResult
        {
            Success = true,
            ExitCode = ExecutionOutcomeCode.Success,
            Message = message,
            Warnings = validation.Warnings,
            Data = data,
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
            RepeatDelayMs = request.RepeatDelayMs,
            MotionMode = request.MotionMode,
            StrictSpeedMotionEventsPerSecond = request.StrictSpeedMotionEventsPerSecond,
            PrecisionMotionEventsPerSecond = request.PrecisionMotionEventsPerSecond,
            MaximumMotionErrorPixels = request.MaximumMotionErrorPixels,
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

        var load = await LoadMacroAsync(macroFilePath).ConfigureAwait(false);
        if (load.Failure is { } loadFailure)
        {
            return loadFailure;
        }

        var macro = load.Macro!;

        var validation = _validator.Validate(macro);
        if (!validation.IsValid)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = ExecutionOutcomeCode.ValidationError,
                Message = "Macro validation failed.",
                Errors = validation.Errors,
                Warnings = validation.Warnings,
                Data = new MacroValidationData(macroFilePath, macro.EventCount),
            };
        }

        if (dryRun)
        {
            return new MacroExecutionResult
            {
                Success = true,
                ExitCode = ExecutionOutcomeCode.Success,
                Message = "Macro is valid.",
                Warnings = validation.Warnings,
                Data = BuildSummaryData(macroFilePath, macro),
            };
        }

        try
        {
            if (countdownSeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(countdownSeconds), _timeProvider, cancellationToken).ConfigureAwait(false);
            }

            using var player = _macroPlayerFactory();
            using var stopRegistration = cancellationToken.Register(() =>
            {
                try
                {
                    player.StopPlayback();
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Debug(ex, "[MacroExecutionService] Failed to stop playback after cancellation");
                }
            });

            await player.PlayAsync(macro, options, cancellationToken).ConfigureAwait(false);

            return new MacroExecutionResult
            {
                Success = true,
                ExitCode = ExecutionOutcomeCode.Success,
                Message = "Playback complete.",
                Warnings = validation.Warnings,
                Data = BuildSummaryData(macroFilePath, macro),
            };
        }
        catch (OperationCanceledException)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = ExecutionOutcomeCode.Cancelled,
                Message = "Playback cancelled.",
            };
        }
        catch (AbsolutePlaybackUnsupportedException)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = ExecutionOutcomeCode.RuntimeError,
                Message = "Absolute coordinate playback is not supported in this session.",
                Errors = ["This macro contains absolute mouse coordinates, but the active backend cannot play absolute coordinates. Use a backend/session with absolute coordinate support or edit the macro to use relative coordinates."],
                Warnings = validation.Warnings,
                Data = BuildSummaryData(macroFilePath, macro),
            };
        }
        catch (InputInjectionPermissionRequiredException ex)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = ExecutionOutcomeCode.EnvironmentError,
                Message = "Playback permission is missing.",
                Errors = [ex.Message],
                Warnings = validation.Warnings,
                Data = BuildSummaryData(macroFilePath, macro),
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new MacroExecutionResult
            {
                Success = false,
                ExitCode = ExecutionOutcomeCode.RuntimeError,
                Message = "Playback failed.",
                Errors = [ex.Message],
                Warnings = validation.Warnings,
                Data = BuildSummaryData(macroFilePath, macro),
            };
        }
    }

    private static MacroSummaryData BuildSummaryData(string macroFilePath, MacroSequence macro)
    {
        var coordinateMode = CoordinateModeText(macro);

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

        var coordinateMode = CoordinateModeText(macro);

        var eventBreakdown = new MacroEventBreakdownData(
            macro.Events.Count(e => e.Type is EventType.MouseMove),
            macro.Events.Count(e => e.Type is EventType.ButtonPress),
            macro.Events.Count(e => e.Type is EventType.ButtonRelease),
            macro.Events.Count(e => e.Type is EventType.Click),
            macro.Events.Count(e => e.Type is EventType.KeyPress),
            macro.Events.Count(e => e.Type is EventType.KeyRelease)
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
            macro.TrailingDelayMicroseconds,
            macro.TrailingDelayMs,
            macro.HasTrailingRandomDelay,
            macro.TrailingDelayMinMs,
            macro.TrailingDelayMaxMs,
            eventBreakdown
        );
    }

    private static string CoordinateModeText(MacroSequence macro) =>
        MacroPositionSemantics.GetCoordinateModeSummary(macro) switch
        {
            CoordinateModeSummary.Absolute => "absolute",
            CoordinateModeSummary.Relative => "relative",
            CoordinateModeSummary.Mixed => "mixed",
            CoordinateModeSummary.None => "none",
            _ => "none",
        };

    private async Task<MacroLoad> LoadMacroAsync(string macroFilePath)
    {
        if (string.IsNullOrWhiteSpace(macroFilePath) || !File.Exists(macroFilePath))
        {
            return MacroLoad.Failed(new MacroExecutionResult
            {
                Success = false,
                ExitCode = ExecutionOutcomeCode.FileError,
                Message = "Macro file not found.",
                Errors = [$"File does not exist: {macroFilePath}"],
            });
        }

        MacroSequence? macro;
        try
        {
            macro = await _macroFileManager.LoadAsync(macroFilePath).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return MacroLoad.Failed(new MacroExecutionResult
            {
                Success = false,
                ExitCode = ExecutionOutcomeCode.FileError,
                Message = "Failed to read macro file.",
                Errors = [ex.Message],
            });
        }

        return macro is null
            ? MacroLoad.Failed(new MacroExecutionResult
            {
                Success = false,
                ExitCode = ExecutionOutcomeCode.ValidationError,
                Message = "Macro file could not be loaded.",
            })
            : MacroLoad.Loaded(macro);
    }

    /// <summary>A loaded macro, or the failure that explains why it could not be loaded.</summary>
    private sealed record MacroLoad(MacroSequence? Macro, MacroExecutionResult? Failure)
    {
        public static MacroLoad Loaded(MacroSequence macro) => new(macro, Failure: null);

        public static MacroLoad Failed(MacroExecutionResult failure) => new(Macro: null, failure);
    }
}
