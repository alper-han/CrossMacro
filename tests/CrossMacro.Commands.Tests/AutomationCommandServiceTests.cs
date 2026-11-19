using CrossMacro.Commands.Execution;
using CrossMacro.Commands.Runtime;
using CrossMacro.Core.Models.Playback;
using Microsoft.Extensions.Time.Testing;

namespace CrossMacro.Commands.Tests;

public sealed class AutomationCommandServiceTests
{
    private readonly IMacroExecutionService _macros = Substitute.For<IMacroExecutionService>();
    private readonly IRunScriptExecutionService _scripts = Substitute.For<IRunScriptExecutionService>();
    private readonly IRecordExecutionService _recording = Substitute.For<IRecordExecutionService>();
    private readonly IPreflightService _preflight = Substitute.For<IPreflightService>();
    private readonly FakeTimeProvider _clock = new();
    private readonly AutomationCommandService _service;

    public AutomationCommandServiceTests()
    {
        _ = _preflight.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("Preflight checks passed."));
        _ = _macros.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("Playback complete."));
        _ = _scripts.ExecuteAsync(Arg.Any<RunScriptExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("Run complete."));
        _ = _recording.ExecuteAsync(Arg.Any<RecordExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult.Ok("Recording completed."));
        _service = new AutomationCommandService(_macros, _scripts, _recording, _preflight, _clock);
    }

    private static OperationResult Preflight(string error) =>
        OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, "Preflight check failed.", [error]);

    private static async Task<OperationResult> NeverCompletesAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken);
        throw new InvalidOperationException("Expected cancellation");
    }

    private void WaitForCancellationThenReport(string message) =>
        _ = _macros.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, call.Arg<CancellationToken>());
                }
                catch (OperationCanceledException)
                {
                    return OperationResult.Fail(ExecutionOutcomeCode.Cancelled, message);
                }

                throw new InvalidOperationException("Expected cancellation");
            });

    [Fact]
    public async Task PlayAsync_MapsOptionsToTheExecutionRequest()
    {
        var options = new PlayOptions(
            "/tmp/test.macro",
            SpeedMultiplier: 2.5,
            Loop: false,
            RepeatCount: 50,
            RepeatDelayMs: 40,
            MotionMode: MotionPlaybackMode.StrictSpeed,
            StrictSpeedMotionEventsPerSecond: 300,
            PrecisionMotionEventsPerSecond: 200,
            MaximumMotionErrorPixels: 1.5,
            CountdownSeconds: 3);
        using var cancellation = new CancellationTokenSource();

        var result = await _service.PlayAsync(options, cancellation.Token);

        Assert.True(result.Success);
        _ = await _preflight.Received(1).CheckAsync(PreflightTarget.Play, cancellation.Token);
        _ = await _macros.Received(1).ExecuteAsync(
            Arg.Is<MacroExecutionRequest>(request => request.MacroFilePath == options.MacroFilePath
                && Math.Abs(request.SpeedMultiplier - 2.5) < 0.000001
                && request.Loop
                && request.RepeatCount == 50
                && request.RepeatDelayMs == 40
                && request.MotionMode == MotionPlaybackMode.StrictSpeed
                && request.StrictSpeedMotionEventsPerSecond == 300
                && request.PrecisionMotionEventsPerSecond == 200
                && Math.Abs(request.MaximumMotionErrorPixels - 1.5) < 0.000001
                && request.CountdownSeconds == 3
                && !request.DryRun),
            cancellation.Token);
    }

    [Fact]
    public async Task PlayAsync_WhenDryRun_SkipsPreflight()
    {
        _ = _preflight.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>()).Returns(Preflight("simulator unsupported"));

        var result = await _service.PlayAsync(new PlayOptions("/tmp/test.macro", DryRun: true), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _preflight.DidNotReceive().CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>());
        _ = await _macros.Received(1).ExecuteAsync(Arg.Is<MacroExecutionRequest>(request => request.DryRun), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayAsync_WhenPreflightFails_ReturnsPreflightResultWithoutExecuting()
    {
        var failure = Preflight("display unsupported");
        _ = _preflight.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await _service.PlayAsync(new PlayOptions("/tmp/test.macro"), CancellationToken.None);

        Assert.Same(failure, result);
        _ = await _macros.DidNotReceive().ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayAsync_WhenExecutionFails_ReturnsTheFailure()
    {
        var failure = OperationResult.Fail(ExecutionOutcomeCode.RuntimeError, "Playback failed.", ["simulator error"]);
        _ = _macros.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await _service.PlayAsync(new PlayOptions("/tmp/test.macro"), CancellationToken.None);

        Assert.Same(failure, result);
    }

    [Fact]
    public async Task PlayAsync_WhenTimeoutExpires_CancelsPlaybackAndReturnsItsCancellation()
    {
        WaitForCancellationThenReport("Playback cancelled.");

        var pending = _service.PlayAsync(new PlayOptions("/tmp/test.macro", TimeoutSeconds: 30), CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(pending.IsCompleted);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var result = await pending;

        Assert.Equal(ExecutionOutcomeCode.Cancelled, result.Outcome);
        Assert.Equal("Playback cancelled.", result.Message);
    }

    [Fact]
    public async Task PlayAsync_WithoutTimeout_PassesTheCallerTokenThrough()
    {
        using var cancellation = new CancellationTokenSource();

        _ = await _service.PlayAsync(new PlayOptions("/tmp/test.macro", TimeoutSeconds: 0), cancellation.Token);

        _ = await _macros.Received(1).ExecuteAsync(Arg.Any<MacroExecutionRequest>(), cancellation.Token);
    }

    [Fact]
    public async Task RunAsync_MapsOptionsToTheScriptRequest()
    {
        var options = new RunOptions(
            ["move abs 10 10", "click left"],
            StepFilePath: "/tmp/steps.txt",
            SpeedMultiplier: 1.5,
            CountdownSeconds: 2,
            DryRun: true,
            ImageAssets: [new RunImageAssetRequest("button", "/tmp/button.png")]);
        using var cancellation = new CancellationTokenSource();

        var result = await _service.RunAsync(options, cancellation.Token);

        Assert.True(result.Success);
        _ = await _scripts.Received(1).ExecuteAsync(
            Arg.Is<RunScriptExecutionRequest>(request => request.Steps.SequenceEqual(options.Steps)
                && request.StepFilePath == options.StepFilePath
                && Math.Abs(request.SpeedMultiplier - 1.5) < 0.000001
                && request.CountdownSeconds == 2
                && request.DryRun
                && request.ImageAssets.Count == 1
                && request.ImageAssets[0].Name == "button"
                && request.ImageAssets[0].FilePath == "/tmp/button.png"),
            cancellation.Token);
    }

    [Fact]
    public async Task RunAsync_WithoutImageAssets_SendsAnEmptyAssetList()
    {
        _ = await _service.RunAsync(new RunOptions(["click left"]), CancellationToken.None);

        _ = await _scripts.Received(1).ExecuteAsync(
            Arg.Is<RunScriptExecutionRequest>(request => request.ImageAssets.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenDryRun_SkipsPreflight()
    {
        _ = _preflight.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>()).Returns(Preflight("simulator unsupported"));

        var result = await _service.RunAsync(new RunOptions(["click left"], DryRun: true), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _preflight.DidNotReceive().CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenPreflightFails_ReturnsPreflightResultWithoutExecuting()
    {
        var failure = Preflight("simulator unsupported");
        _ = _preflight.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await _service.RunAsync(new RunOptions(["click left"]), CancellationToken.None);

        Assert.Same(failure, result);
        _ = await _preflight.Received(1).CheckAsync(PreflightTarget.Run, CancellationToken.None);
        _ = await _scripts.DidNotReceive().ExecuteAsync(Arg.Any<RunScriptExecutionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WhenExecutionFails_ReturnsTheFailure()
    {
        var failure = OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Run script parsing failed.", ["Step 1: bad syntax"]);
        _ = _scripts.ExecuteAsync(Arg.Any<RunScriptExecutionRequest>(), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await _service.RunAsync(new RunOptions(["bad-step"]), CancellationToken.None);

        Assert.Same(failure, result);
        _ = await _preflight.Received(1).CheckAsync(PreflightTarget.Run, CancellationToken.None);
    }

    [Fact]
    public async Task RecordAsync_PreflightsThenMapsOptionsToTheRecordingRequest()
    {
        var options = new RecordOptions(
            "/tmp/out.macro",
            RecordMouse: false,
            RecordKeyboard: true,
            CoordinateMode: RecordCoordinateMode.Absolute,
            SkipInitialZero: true,
            DurationSeconds: 12);
        using var cancellation = new CancellationTokenSource();

        var result = await _service.RecordAsync(options, cancellation.Token);

        Assert.True(result.Success);
        _ = await _preflight.Received(1).CheckAsync(PreflightTarget.Record, cancellation.Token);
        _ = await _recording.Received(1).ExecuteAsync(
            Arg.Is<RecordExecutionRequest>(request => request.OutputFilePath == options.OutputFilePath
                && !request.RecordMouse
                && request.RecordKeyboard
                && request.CoordinateMode == RecordCoordinateMode.Absolute
                && request.SkipInitialZero
                && request.DurationSeconds == 12),
            cancellation.Token);
    }

    [Fact]
    public async Task RecordAsync_WhenPreflightFails_ReturnsPreflightResultWithoutRecording()
    {
        var failure = Preflight("capture backend unavailable");
        _ = _preflight.CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await _service.RecordAsync(new RecordOptions("/tmp/out.macro"), CancellationToken.None);

        Assert.Same(failure, result);
        _ = await _recording.DidNotReceive().ExecuteAsync(Arg.Any<RecordExecutionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordAsync_WhenRecordingFails_ReturnsTheFailure()
    {
        var failure = OperationResult.Fail(ExecutionOutcomeCode.EnvironmentError, "Failed to start recording.", ["capture unavailable"]);
        _ = _recording.ExecuteAsync(Arg.Any<RecordExecutionRequest>(), Arg.Any<CancellationToken>()).Returns(failure);

        var result = await _service.RecordAsync(new RecordOptions("/tmp/out.macro"), CancellationToken.None);

        Assert.Same(failure, result);
    }

    [Fact]
    public async Task PlayBoundedAsync_WhenDeadlineExpires_ReportsTimeoutUsingTheInjectedClock()
    {
        WaitForCancellationThenReport("Playback cancelled.");

        var pending = _service.PlayBoundedAsync(new PlayOptions("macro", TimeoutSeconds: 30), CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(pending.IsCompleted);
        _clock.Advance(TimeSpan.FromSeconds(1));
        var result = await pending;

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.RuntimeError, result.Outcome);
        Assert.Equal("Automation operation timed out.", result.Message);
        _ = await _preflight.DidNotReceive().CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayBoundedAsync_WhenCallerCancels_StaysCancellationInsteadOfTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        _ = _macros.ExecuteAsync(Arg.Any<MacroExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => NeverCompletesAsync(call.Arg<CancellationToken>()));

        var pending = _service.PlayBoundedAsync(new PlayOptions("macro", TimeoutSeconds: 30), cancellation.Token);
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task RunBoundedAsync_WhenDeadlineExpires_ReportsTimeout()
    {
        _ = _scripts.ExecuteAsync(Arg.Any<RunScriptExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => NeverCompletesAsync(call.Arg<CancellationToken>()));

        var pending = _service.RunBoundedAsync(new RunOptions(["click left"], TimeoutSeconds: 5), CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(5));
        var result = await pending;

        Assert.Equal(ExecutionOutcomeCode.RuntimeError, result.Outcome);
        Assert.Equal("Automation operation timed out.", result.Message);
    }

    [Fact]
    public async Task BoundedExecution_RequiresAPositiveTimeout()
    {
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.PlayBoundedAsync(new PlayOptions("macro", TimeoutSeconds: 0), CancellationToken.None));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.RunBoundedAsync(new RunOptions(["click left"], TimeoutSeconds: 0), CancellationToken.None));
    }

    [Fact]
    public async Task RecordBoundedAsync_RecordsWithoutPreflight()
    {
        var result = await _service.RecordBoundedAsync(new RecordOptions("/tmp/out.macro", DurationSeconds: 5), CancellationToken.None);

        Assert.True(result.Success);
        _ = await _preflight.DidNotReceive().CheckAsync(Arg.Any<PreflightTarget>(), Arg.Any<CancellationToken>());
        _ = await _recording.Received(1).ExecuteAsync(Arg.Is<RecordExecutionRequest>(request => request.DurationSeconds == 5), Arg.Any<CancellationToken>());
    }
}
