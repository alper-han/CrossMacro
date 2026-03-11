using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using NSubstitute;
using System.Text.Json;

namespace CrossMacro.Cli.Tests;

public class RecordExecutionServiceTests
{
    private readonly IMacroRecorder _macroRecorder;
    private readonly IMacroFileManager _macroFileManager;
    private readonly IMousePositionProvider _mousePositionProvider;
    private readonly IRecordExecutionService _service;

    public RecordExecutionServiceTests()
    {
        _macroRecorder = Substitute.For<IMacroRecorder>();
        _macroFileManager = Substitute.For<IMacroFileManager>();
        _mousePositionProvider = Substitute.For<IMousePositionProvider>();
        _service = new RecordExecutionService(_macroRecorder, _macroFileManager, _mousePositionProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAbsoluteRequestedButUnsupported_FallsBackToRelativeWithWarning()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(CreateSequenceWithOneEvent());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        var result = await _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-abs-fallback.macro",
            CoordinateMode = RecordCoordinateMode.Absolute
        }, cts.Token);

        Assert.True(result.Success);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
        Assert.Contains(result.Warnings, x => x.Contains("Falling back to relative mode.", StringComparison.Ordinal));

        await _macroRecorder.Received(1).StartRecordingAsync(
            true,
            true,
            Arg.Any<IEnumerable<int>>(),
            forceRelative: true,
            skipInitialZero: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAutoAndAbsoluteSupported_UsesAbsolute()
    {
        _mousePositionProvider.IsSupported.Returns(true);
        _mousePositionProvider.GetAbsolutePositionAsync().Returns((100, 200));
        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(CreateSequenceWithOneEvent());

        var result = await _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-auto-abs.macro",
            CoordinateMode = RecordCoordinateMode.Auto,
            DurationSeconds = 0
        }, new CancellationTokenSource(10).Token);

        Assert.True(result.Success);
        await _macroRecorder.Received(1).StartRecordingAsync(
            true,
            true,
            Arg.Any<IEnumerable<int>>(),
            forceRelative: false,
            skipInitialZero: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRecordedSequenceFallsBackToRelative_DataReflectsEffectiveMode()
    {
        _mousePositionProvider.IsSupported.Returns(true);
        _mousePositionProvider.GetAbsolutePositionAsync().Returns((100, 200));
        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(new MacroSequence
        {
            Name = "recorded",
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 1,
                    Y = 1,
                    Timestamp = 0
                }
            }
        });

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        var result = await _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-effective-mode.macro",
            CoordinateMode = RecordCoordinateMode.Auto
        }, cts.Token);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var payload = JsonSerializer.SerializeToElement(result.Data);
        Assert.Equal("relative", payload.GetProperty("actualMode").GetString());
        Assert.True(payload.GetProperty("skipInitialZero").GetBoolean());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoEventsRecorded_ReturnsRuntimeError()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(new MacroSequence());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(10);

        var result = await _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-empty.macro"
        }, cts.Token);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.RuntimeError, result.ExitCode);
        Assert.Contains("No events were recorded", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartTaskDoesNotComplete_ReturnsCancelled()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        var blockingStartTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(blockingStartTask.Task);

        _macroRecorder.IsRecording.Returns(false);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(25);

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-blocking-start.macro"
        }, cts.Token);

        var completed = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(executeTask, completed);

        var result = await executeTask;
        Assert.False(result.Success);
        Assert.Equal(CliExitCode.Cancelled, result.ExitCode);
        Assert.Contains("cancelled before start", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartTaskFaults_ReturnsEnvironmentError()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("start failed")));

        var result = await _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-start-fail.macro"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("Failed to start recording", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Errors, x => string.Equals(x, "start failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenStartTaskFaultsAfterProbe_ReturnsEnvironmentErrorWithoutExternalCancellation()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        var delayedStartTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(delayedStartTask.Task);

        _macroRecorder.IsRecording.Returns(false);

        _ = Task.Run(async () =>
        {
            await Task.Delay(400);
            delayedStartTask.TrySetException(new InvalidOperationException("late start failed"));
        });

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-late-start-fail.macro"
        }, CancellationToken.None);

        var completed = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(executeTask, completed);

        var result = await executeTask;
        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("Failed to start recording", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Errors, x => string.Equals(x, "late start failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledThenStartupFaults_ReturnsEnvironmentError()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        var delayedStartTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(delayedStartTask.Task);

        _macroRecorder.IsRecording.Returns(false);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(25);

        _ = Task.Run(async () =>
        {
            await Task.Delay(80);
            delayedStartTask.TrySetException(new InvalidOperationException("fault after cancellation"));
        });

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-cancel-then-fault.macro"
        }, cts.Token);

        var completed = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(executeTask, completed);

        var result = await executeTask;
        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("Failed to start recording", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Errors, x => string.Equals(x, "fault after cancellation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledButEventsCapturedAndStartupSettlesLate_PreservesSuccessfulRecording()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        var neverCompletingStartTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(neverCompletingStartTask.Task);

        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(CreateSequenceWithOneEvent());

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(25);

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-cancel-with-captured-events.macro"
        }, cts.Token);

        var completed = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(executeTask, completed);

        var result = await executeTask;
        Assert.True(result.Success);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledBeforeStartAndStopFails_ReturnsRuntimeError()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        var blockingStartTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(blockingStartTask.Task);

        _macroRecorder.IsRecording.Returns(true);
        _macroRecorder.StopRecording().Returns(_ => throw new InvalidOperationException("stop failed"));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(25);

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-cancel-stop-fail.macro"
        }, cts.Token);

        var completed = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(executeTask, completed);

        var result = await executeTask;
        Assert.False(result.Success);
        Assert.Equal(CliExitCode.RuntimeError, result.ExitCode);
        Assert.Contains("while stopping", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Errors, x => string.Equals(x, "stop failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledBeforeStartAndRecorderStopsConcurrently_ReturnsCancelled()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        var blockingStartTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(blockingStartTask.Task);

        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(_ => throw new InvalidOperationException("Not currently recording"));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(25);

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-cancel-race-stop-transition.macro"
        }, cts.Token);

        var completed = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(executeTask, completed);

        var result = await executeTask;
        Assert.False(result.Success);
        Assert.Equal(CliExitCode.Cancelled, result.ExitCode);
        Assert.Contains("cancelled before start", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledDuringSlowStartupThatLaterSucceeds_ReturnsCancelled()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        var delayedStartTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                startInvoked.TrySetResult();
                return delayedStartTask.Task;
            });

        _macroRecorder.IsRecording.Returns(false);

        using var cts = new CancellationTokenSource();

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-cancel-then-succeed.macro"
        }, cts.Token);

        await startInvoked.Task;
        cts.Cancel();

        delayedStartTask.TrySetResult();

        var completed = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.Same(executeTask, completed);

        var result = await executeTask;
        Assert.False(result.Success);
        Assert.Equal(CliExitCode.Cancelled, result.ExitCode);
        Assert.Contains("cancelled before start", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDurationRequested_WaitsUntilStartupCompletesBeforeStopping()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        var delayedStartTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var isRecording = false;

        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(delayedStartTask.Task);

        _macroRecorder.IsRecording.Returns(_ => isRecording);
        _macroRecorder.StopRecording().Returns(_ =>
        {
            isRecording = false;
            stopCalled.TrySetResult();
            return CreateSequenceWithOneEvent();
        });

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-duration-after-startup.macro",
            DurationSeconds = 1
        }, CancellationToken.None);

        await Task.Delay(900);
        isRecording = true;
        delayedStartTask.TrySetResult();

        var prematureStop = await Task.WhenAny(stopCalled.Task, Task.Delay(250));
        Assert.NotSame(stopCalled.Task, prematureStop);

        var completed = await Task.WhenAny(executeTask, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(executeTask, completed);

        var result = await executeTask;
        Assert.True(result.Success);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
    }

    private static MacroSequence CreateSequenceWithOneEvent()
    {
        var sequence = new MacroSequence
        {
            Name = "recorded"
        };
        sequence.Events.Add(new MacroEvent
        {
            Type = EventType.MouseMove,
            X = 1,
            Y = 1,
            Timestamp = 0
        });
        sequence.CalculateDuration();
        return sequence;
    }
}
