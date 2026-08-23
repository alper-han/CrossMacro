namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSInputEventDispatcherTests
{
    [Fact]
    public void Dispose_DrainsEventsInOrderOnOwnedThread()
    {
        var values = new List<int>();
        int producerThread = Environment.CurrentManagedThreadId;
        int dispatcherThread = producerThread;
        using (var dispatcher = new MacOSInputEventDispatcher(
            inputEvent =>
            {
                dispatcherThread = Environment.CurrentManagedThreadId;
                values.Add(inputEvent.Value);
            },
            static _ => Assert.Fail("No dispatch error was expected.")))
        {
            Assert.True(dispatcher.TryEnqueue(CreateInput(1)));
            Assert.True(dispatcher.TryEnqueue(CreateInput(2)));
            Assert.True(dispatcher.TryEnqueue(CreateInput(3)));
        }

        Assert.Equal([1, 2, 3], values);
        Assert.NotEqual(producerThread, dispatcherThread);
    }

    [Fact]
    public void TryEnqueue_WhenSubscriberIsSlow_DoesNotBlockProducer()
    {
        using var entered = new ManualResetEventSlim(initialState: false);
        using var release = new ManualResetEventSlim(initialState: false);
        using var dispatcher = new MacOSInputEventDispatcher(
            inputEvent =>
            {
                entered.Set();
                _ = release.Wait(TimeSpan.FromSeconds(2), CancellationToken.None);
            },
            static _ => Assert.Fail("No dispatch error was expected."));

        try
        {
            Assert.True(dispatcher.TryEnqueue(CreateInput(1)));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(1), CancellationToken.None));

            var stopwatch = Stopwatch.StartNew();
            Assert.True(dispatcher.TryEnqueue(CreateInput(2)));
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(250));
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public void TryEnqueue_WhenQueueIsFull_ReturnsFalseInsteadOfBlocking()
    {
        using var entered = new ManualResetEventSlim(initialState: false);
        using var release = new ManualResetEventSlim(initialState: false);
        using var dispatcher = new MacOSInputEventDispatcher(
            inputEvent =>
            {
                entered.Set();
                _ = release.Wait(TimeSpan.FromSeconds(2), CancellationToken.None);
            },
            static _ => Assert.Fail("No dispatch error was expected."),
            capacity: 1);

        try
        {
            Assert.True(dispatcher.TryEnqueue(CreateInput(1)));
            Assert.True(entered.Wait(TimeSpan.FromSeconds(1), CancellationToken.None));
            Assert.True(dispatcher.TryEnqueue(CreateInput(2)));

            var stopwatch = Stopwatch.StartNew();
            Assert.False(dispatcher.TryEnqueue(CreateInput(3)));
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(250));
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public void DispatchLoop_WhenSubscriberThrows_ReportsErrorAndContinues()
    {
        using var secondEvent = new ManualResetEventSlim(initialState: false);
        var errors = new List<Exception>();
        using var dispatcher = new MacOSInputEventDispatcher(
            inputEvent =>
            {
                if (inputEvent.Value is 1)
                {
                    throw new InvalidOperationException("subscriber failed");
                }

                secondEvent.Set();
            },
            errors.Add);

        Assert.True(dispatcher.TryEnqueue(CreateInput(1)));
        Assert.True(dispatcher.TryEnqueue(CreateInput(2)));
        Assert.True(secondEvent.Wait(TimeSpan.FromSeconds(1), CancellationToken.None));
        Assert.Contains(errors, static error => error.Message is "subscriber failed");
    }

    [Fact]
    public void Dispose_StopsAcceptanceAndCompletesWorker()
    {
        var dispatcher = new MacOSInputEventDispatcher(
            static _ => { },
            static _ => { });

        dispatcher.Dispose();
        dispatcher.Dispose();

        Assert.True(dispatcher.IsCompleted);
        Assert.False(dispatcher.TryEnqueue(CreateInput(1)));
    }

    [Fact]
    public async Task Dispose_WhenSubscriberIsActive_WaitsForCallbackQuiescence()
    {
        using var entered = new ManualResetEventSlim(initialState: false);
        using var release = new ManualResetEventSlim(initialState: false);
        var dispatcher = new MacOSInputEventDispatcher(
            inputEvent =>
            {
                entered.Set();
                _ = release.Wait(TimeSpan.FromSeconds(2), CancellationToken.None);
            },
            static _ => { });
        Assert.True(dispatcher.TryEnqueue(CreateInput(1)));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(1), CancellationToken.None));

        Task disposeTask = Task.Run(dispatcher.Dispose, CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, CancellationToken.None);
        Assert.False(disposeTask.IsCompleted);

        release.Set();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(1), TimeProvider.System, CancellationToken.None);
        Assert.True(dispatcher.IsCompleted);
    }

    [Fact]
    public void Dispose_WhenCalledFromSubscriber_DoesNotDeadlock()
    {
        using var completed = new ManualResetEventSlim(initialState: false);
        MacOSInputEventDispatcher? dispatcherReference = null;
        var dispatcher = new MacOSInputEventDispatcher(
            _ =>
            {
                dispatcherReference!.Dispose();
                completed.Set();
            },
            static _ => { });
        dispatcherReference = dispatcher;

        Assert.True(dispatcher.TryEnqueue(CreateInput(1)));
        Assert.True(completed.Wait(TimeSpan.FromSeconds(1), CancellationToken.None));
        Assert.True(SpinWait.SpinUntil(() => dispatcher.IsCompleted, TimeSpan.FromSeconds(1)));
    }

    private static CapturedInputEventArgs CreateInput(int value) => new()
    {
        Type = InputEventType.Key,
        Code = InputEventCode.KEY_A,
        Value = value,
    };
}
