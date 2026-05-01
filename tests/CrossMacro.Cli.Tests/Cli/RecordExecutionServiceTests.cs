using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using CrossMacro.TestInfrastructure;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Cli.Tests;

public class RecordExecutionServiceTests
{
    private readonly IMacroRecorder _macroRecorder;
    private readonly IMacroFileManager _macroFileManager;
    private readonly IMousePositionProvider _mousePositionProvider;
    private readonly ControlledDelay _delay;
    private readonly IRecordExecutionService _service;

    public RecordExecutionServiceTests()
    {
        _macroRecorder = Substitute.For<IMacroRecorder>();
        _macroFileManager = Substitute.For<IMacroFileManager>();
        _mousePositionProvider = Substitute.For<IMousePositionProvider>();
        _delay = new ControlledDelay();
        _service = new RecordExecutionService(_macroRecorder, _macroFileManager, _mousePositionProvider, _delay.DelayAsync);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAbsoluteRequestedButUnsupported_FallsBackToRelativeWithWarning()
    {
        _mousePositionProvider.IsSupported.Returns(false);
        ConfigureImmediateStart();
        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(CreateSequenceWithOneEvent());

        using var cts = new CancellationTokenSource();

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-abs-fallback.macro",
            CoordinateMode = RecordCoordinateMode.Absolute
        }, cts.Token);

        await CancelAfterStartupWaitIsObservedAsync(cts);
        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));

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
        ConfigureImmediateStart();
        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(CreateSequenceWithOneEvent());

        using var cts = new CancellationTokenSource();

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-auto-abs.macro",
            CoordinateMode = RecordCoordinateMode.Auto,
            DurationSeconds = 0
        }, cts.Token);

        await CancelAfterStartupWaitIsObservedAsync(cts);
        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));

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
        ConfigureImmediateStart();
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

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-effective-mode.macro",
            CoordinateMode = RecordCoordinateMode.Auto
        }, cts.Token);

        await CancelAfterStartupWaitIsObservedAsync(cts);
        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));

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
        ConfigureImmediateStart();
        _macroRecorder.IsRecording.Returns(true, false);
        _macroRecorder.StopRecording().Returns(new MacroSequence());

        using var cts = new CancellationTokenSource();

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-empty.macro"
        }, cts.Token);

        await CancelAfterStartupWaitIsObservedAsync(cts);
        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));

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

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-blocking-start.macro"
        }, cts.Token);

        var startupWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Timeout.InfiniteTimeSpan, startupWait.Duration);

        cts.Cancel();

        var settleWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromMilliseconds(300), settleWait.Duration);
        settleWait.Complete();

        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
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
        var startInvoked = new AsyncSignal();
        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                startInvoked.Signal();
                return delayedStartTask.Task;
            });

        _macroRecorder.IsRecording.Returns(false);

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-late-start-fail.macro"
        }, CancellationToken.None);

        await startInvoked.WaitAsync(TimeSpan.FromSeconds(2));
        delayedStartTask.TrySetException(new InvalidOperationException("late start failed"));

        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-cancel-then-fault.macro"
        }, cts.Token);

        var startupWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Timeout.InfiniteTimeSpan, startupWait.Duration);

        cts.Cancel();

        var settleWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromMilliseconds(300), settleWait.Duration);

        delayedStartTask.TrySetException(new InvalidOperationException("fault after cancellation"));

        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-cancel-with-captured-events.macro"
        }, cts.Token);

        var startupWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Timeout.InfiniteTimeSpan, startupWait.Duration);

        cts.Cancel();

        var settleWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromMilliseconds(300), settleWait.Duration);
        settleWait.Complete();

        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-cancel-stop-fail.macro"
        }, cts.Token);

        var startupWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Timeout.InfiniteTimeSpan, startupWait.Duration);

        cts.Cancel();

        var settleWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromMilliseconds(300), settleWait.Duration);
        settleWait.Complete();

        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        var executeTask = _service.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = "/tmp/test-record-cancel-race-stop-transition.macro"
        }, cts.Token);

        var startupWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Timeout.InfiniteTimeSpan, startupWait.Duration);

        cts.Cancel();

        var settleWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromMilliseconds(300), settleWait.Duration);
        settleWait.Complete();

        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        await startInvoked.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var startupWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Timeout.InfiniteTimeSpan, startupWait.Duration);

        cts.Cancel();

        var settleWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromMilliseconds(300), settleWait.Duration);

        delayedStartTask.TrySetResult();

        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
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

        var startupWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Timeout.InfiniteTimeSpan, startupWait.Duration);
        Assert.False(stopCalled.Task.IsCompleted);

        isRecording = true;
        delayedStartTask.TrySetResult();

        var durationWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(1), durationWait.Duration);
        Assert.False(stopCalled.Task.IsCompleted);

        durationWait.Complete();

        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));
        await stopCalled.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(result.Success);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
    }

    private void ConfigureImmediateStart()
    {
        _macroRecorder.StartRecordingAsync(
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IEnumerable<int>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private async Task CancelAfterStartupWaitIsObservedAsync(CancellationTokenSource cts)
    {
        var startupWait = await _delay.WaitForNextRequestAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(Timeout.InfiniteTimeSpan, startupWait.Duration);
        cts.Cancel();
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

    private sealed class ControlledDelay
    {
        private readonly object _sync = new();
        private readonly Queue<DelayRequest> _requests = new();
        private readonly AsyncSignal _requestArrived = new();

        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = new DelayRequest(duration, completionSource);

            CancellationTokenRegistration cancellationRegistration = default;
            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(static state =>
                {
                    ((TaskCompletionSource)state!).TrySetCanceled();
                }, completionSource);

                _ = completionSource.Task.ContinueWith(
                    static (_, state) => ((CancellationTokenRegistration)state!).Dispose(),
                    cancellationRegistration,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            lock (_sync)
            {
                _requests.Enqueue(request);
                _requestArrived.Signal();
            }

            return completionSource.Task;
        }

        public async Task<DelayRequest> WaitForNextRequestAsync(TimeSpan timeout)
        {
            while (true)
            {
                lock (_sync)
                {
                    if (_requests.Count > 0)
                    {
                        var request = _requests.Dequeue();
                        if (_requests.Count == 0)
                        {
                            _requestArrived.Reset();
                        }

                        return request;
                    }

                    _requestArrived.Reset();
                }

                await _requestArrived.WaitAsync(timeout);
            }
        }
    }

    private sealed class DelayRequest(TimeSpan duration, TaskCompletionSource completionSource)
    {
        public TimeSpan Duration { get; } = duration;

        public void Complete()
        {
            completionSource.TrySetResult();
        }
    }
}
