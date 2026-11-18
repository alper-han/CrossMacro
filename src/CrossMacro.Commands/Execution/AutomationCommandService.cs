using CrossMacro.Commands.Runtime;

namespace CrossMacro.Commands.Execution;

/// <summary>
/// Preflight, command deadline and request mapping for the play, run and record commands. The CLI handlers
/// use the preflighted entry points; the MCP tools preflight first so they can report the failure before an
/// operation starts, then use the bounded entry points that report an expired deadline as a timed-out result.
/// </summary>
public sealed class AutomationCommandService(
    IMacroExecutionService macros,
    IRunScriptExecutionService scripts,
    IRecordExecutionService recording,
    IPreflightService preflight,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Task<OperationResult> PreflightAsync(PlayOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return PreflightUnlessDryRunAsync(PreflightTarget.Play, options.DryRun, cancellationToken);
    }

    public Task<OperationResult> PreflightAsync(RunOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return PreflightUnlessDryRunAsync(PreflightTarget.Run, options.DryRun, cancellationToken);
    }

    public Task<OperationResult> PreflightAsync(RecordOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return preflight.CheckAsync(PreflightTarget.Record, cancellationToken);
    }

    /// <summary>Preflights, then plays; a positive <c>TimeoutSeconds</c> cancels the playback when it expires.</summary>
    public async Task<OperationResult> PlayAsync(PlayOptions options, CancellationToken cancellationToken)
    {
        var check = await PreflightAsync(options, cancellationToken).ConfigureAwait(false);
        return check.Success
            ? await RunWithDeadlineAsync(options.TimeoutSeconds, reportTimeout: false, token => ExecutePlayAsync(options, token), cancellationToken).ConfigureAwait(false)
            : check;
    }

    /// <summary>Preflights, then runs the script; a positive <c>TimeoutSeconds</c> cancels the run when it expires.</summary>
    public async Task<OperationResult> RunAsync(RunOptions options, CancellationToken cancellationToken)
    {
        var check = await PreflightAsync(options, cancellationToken).ConfigureAwait(false);
        return check.Success
            ? await RunWithDeadlineAsync(options.TimeoutSeconds, reportTimeout: false, token => ExecuteRunAsync(options, token), cancellationToken).ConfigureAwait(false)
            : check;
    }

    public async Task<OperationResult> RecordAsync(RecordOptions options, CancellationToken cancellationToken)
    {
        var check = await PreflightAsync(options, cancellationToken).ConfigureAwait(false);
        return check.Success ? await RecordBoundedAsync(options, cancellationToken).ConfigureAwait(false) : check;
    }

    /// <summary>Plays without preflight; <c>TimeoutSeconds</c> must be positive and expiry yields a timed-out result.</summary>
    public Task<OperationResult> PlayBoundedAsync(PlayOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return RunWithDeadlineAsync(options.TimeoutSeconds, reportTimeout: true, token => ExecutePlayAsync(options, token), cancellationToken);
    }

    /// <summary>Runs the script without preflight; <c>TimeoutSeconds</c> must be positive and expiry yields a timed-out result.</summary>
    public Task<OperationResult> RunBoundedAsync(RunOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return RunWithDeadlineAsync(options.TimeoutSeconds, reportTimeout: true, token => ExecuteRunAsync(options, token), cancellationToken);
    }

    /// <summary>Records without preflight; the recording is bounded by <c>DurationSeconds</c>.</summary>
    public Task<OperationResult> RecordBoundedAsync(RecordOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return recording.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = options.OutputFilePath,
            RecordMouse = options.RecordMouse,
            RecordKeyboard = options.RecordKeyboard,
            CoordinateMode = options.CoordinateMode,
            SkipInitialZero = options.SkipInitialZero,
            DurationSeconds = options.DurationSeconds,
        }, cancellationToken);
    }

    private Task<OperationResult> PreflightUnlessDryRunAsync(PreflightTarget target, bool dryRun, CancellationToken cancellationToken) =>
        dryRun
            ? Task.FromResult(OperationResult.Ok("Preflight skipped for dry run."))
            : preflight.CheckAsync(target, cancellationToken);

    private Task<OperationResult> ExecutePlayAsync(PlayOptions options, CancellationToken cancellationToken) =>
        macros.ExecuteAsync(new MacroExecutionRequest
        {
            MacroFilePath = options.MacroFilePath,
            SpeedMultiplier = options.SpeedMultiplier,
            Loop = options.Loop || options.RepeatCount is not 1,
            RepeatCount = options.RepeatCount,
            RepeatDelayMs = options.RepeatDelayMs,
            MotionMode = options.MotionMode,
            StrictSpeedMotionEventsPerSecond = options.StrictSpeedMotionEventsPerSecond,
            PrecisionMotionEventsPerSecond = options.PrecisionMotionEventsPerSecond,
            MaximumMotionErrorPixels = options.MaximumMotionErrorPixels,
            CountdownSeconds = options.CountdownSeconds,
            DryRun = options.DryRun,
        }, cancellationToken);

    private Task<OperationResult> ExecuteRunAsync(RunOptions options, CancellationToken cancellationToken) =>
        scripts.ExecuteAsync(new RunScriptExecutionRequest
        {
            Steps = options.Steps,
            StepFilePath = options.StepFilePath,
            SpeedMultiplier = options.SpeedMultiplier,
            CountdownSeconds = options.CountdownSeconds,
            DryRun = options.DryRun,
            ImageAssets = options.ImageAssets ?? [],
        }, cancellationToken);

    private async Task<OperationResult> RunWithDeadlineAsync(
        int timeoutSeconds,
        bool reportTimeout,
        Func<CancellationToken, Task<OperationResult>> execute,
        CancellationToken cancellationToken)
    {
        if (!reportTimeout && timeoutSeconds <= 0)
        {
            return await execute(cancellationToken).ConfigureAwait(false);
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeoutSeconds, 0);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds), _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            var result = await execute(linked.Token).ConfigureAwait(false);
            return reportTimeout && DeadlineExpired(deadline, cancellationToken) ? TimedOut() : result;
        }
        catch (OperationCanceledException) when (reportTimeout && DeadlineExpired(deadline, cancellationToken))
        {
            return TimedOut();
        }
    }

    private static bool DeadlineExpired(CancellationTokenSource deadline, CancellationToken cancellationToken) =>
        deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested;

    private static OperationResult TimedOut() =>
        OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "Automation operation timed out.");
}
