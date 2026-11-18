namespace CrossMacro.Daemon.Tests.Services;

internal sealed class FakeInputCaptureManager : IInputCaptureManager
{
    private readonly Lock _sync = new();
    private Action<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>? _onEvent;
    private readonly TaskCompletionSource _captureStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<StopCaptureWaiter> _stopCaptureWaiters = [];
    private bool _emitDuringStart;
    private CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event _emitDuringStartEvent;
    private CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event[]? _emitSequenceDuringStart;
    private bool _emitPreviousAndCurrentOnNextStart;
    private CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event _previousGenerationStartEvent;
    private CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event _currentGenerationStartEvent;
    private CaptureStartResult _startResult = CaptureStartResult.Started(startedDeviceCount: 1);
    private Exception? _startException;

    public int StartCaptureCalls { get; private set; }
    public int StopCaptureCalls { get; private set; }
    public bool LastCaptureMouse { get; private set; }
    public bool LastCaptureKeyboard { get; private set; }

    public CaptureStartResult StartCapture(bool captureMouse, bool captureKeyboard, Action<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event> onEvent)
    {
        if (_startException is not null)
        {
            var exception = _startException;
            _startException = null;
            throw exception;
        }

        StartCaptureCalls++;
        LastCaptureMouse = captureMouse;
        LastCaptureKeyboard = captureKeyboard;
        var previousOnEvent = _onEvent;
        _onEvent = onEvent;
        if (_emitDuringStart)
        {
            onEvent(_emitDuringStartEvent);
        }
        if (_emitSequenceDuringStart is not null)
        {
            foreach (var inputEvent in _emitSequenceDuringStart)
            {
                onEvent(inputEvent);
            }

            _emitSequenceDuringStart = null;
        }
        if (_emitPreviousAndCurrentOnNextStart)
        {
            previousOnEvent?.Invoke(_previousGenerationStartEvent);
            onEvent(_currentGenerationStartEvent);
            _emitPreviousAndCurrentOnNextStart = false;
        }
        _ = _captureStarted.TrySetResult();
        var startResult = _startResult;
        _startResult = CaptureStartResult.Started(startedDeviceCount: 1);
        return startResult;
    }

    public void StopCapture()
    {
        List<StopCaptureWaiter> completedWaiters;
        lock (_sync)
        {
            StopCaptureCalls++;
            completedWaiters = CompleteSatisfiedStopCaptureWaiters();
        }

        foreach (var waiter in completedWaiters)
        {
            waiter.Complete();
        }
    }

    public void Emit(CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event inputEvent)
    {
        _onEvent?.Invoke(inputEvent);
    }

    public Task WaitForStartCaptureAsync(TimeSpan timeout) =>
        _captureStarted.Task.WaitAsync(timeout, TimeProvider.System, CancellationToken.None);

    public async Task WaitForStopCaptureCountAsync(int expectedCount, TimeSpan timeout)
    {
        StopCaptureWaiter waiter;
        lock (_sync)
        {
            if (StopCaptureCalls >= expectedCount)
            {
                return;
            }

            waiter = new StopCaptureWaiter(expectedCount);
            _stopCaptureWaiters.Add(waiter);
        }

        try
        {
            await waiter.Task.WaitAsync(timeout, TimeProvider.System, CancellationToken.None);
        }
        catch (TimeoutException ex)
        {
            lock (_sync)
            {
                _ = _stopCaptureWaiters.Remove(waiter);
            }

            throw new TimeoutException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Timed out waiting for StopCaptureCalls >= {expectedCount}. Current StopCaptureCalls={GetStopCaptureCalls()}."),
                ex);
        }
    }

    private int GetStopCaptureCalls()
    {
        lock (_sync)
        {
            return StopCaptureCalls;
        }
    }

    private List<StopCaptureWaiter> CompleteSatisfiedStopCaptureWaiters()
    {
        var completedWaiters = new List<StopCaptureWaiter>();
        for (var index = _stopCaptureWaiters.Count - 1; index >= 0; index--)
        {
            var waiter = _stopCaptureWaiters[index];
            if (StopCaptureCalls < waiter.ExpectedCount)
            {
                continue;
            }

            _stopCaptureWaiters.RemoveAt(index);
            completedWaiters.Add(waiter);
        }

        return completedWaiters;
    }

    private sealed class StopCaptureWaiter(int expectedCount)
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ExpectedCount { get; } = expectedCount;

        public Task Task => _completion.Task;

        public void Complete() => _completion.TrySetResult();
    }

    public void ConfigureEmitDuringStart(CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event inputEvent)
    {
        _emitDuringStart = true;
        _emitDuringStartEvent = inputEvent;
    }

    public void ConfigureEmitSequenceDuringStart(params CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event[] events)
    {
        _emitSequenceDuringStart = events.Length is 0 ? null : events;
    }

    public void ConfigureEmitPreviousAndCurrentEventsOnNextStart(
        CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event previousGenerationEvent,
        CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event currentGenerationEvent)
    {
        _emitPreviousAndCurrentOnNextStart = true;
        _previousGenerationStartEvent = previousGenerationEvent;
        _currentGenerationStartEvent = currentGenerationEvent;
    }

    public void ConfigureStartFailure(string errorMessage)
    {
        _startResult = CaptureStartResult.Failed(errorMessage);
    }

    public void ConfigureStartException(Exception exception)
    {
        _startException = exception;
    }

    public void Dispose()
    {
    }
}
