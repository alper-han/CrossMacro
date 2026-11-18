namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class InputEventDispatcherTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public void Dispose_DrainsEventsInOrderOnOwnedThread()
    {
        var values = new List<int>();
        int producerThread = Environment.CurrentManagedThreadId;
        int dispatcherThread = producerThread;
        using (var dispatcher = new InputEventDispatcher(
            "test-dispatch",
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
    public async Task TryEnqueue_WhenSubscriberIsBlocked_ReturnsWithoutWaitingForSubscriber()
    {
        using var entered = new ManualResetEventSlim(initialState: false);
        using var release = new ManualResetEventSlim(initialState: false);
        using var dispatcher = new InputEventDispatcher(
            "test-dispatch",
            inputEvent =>
            {
                entered.Set();
                _ = release.Wait(Timeout, CancellationToken.None);
            },
            static _ => Assert.Fail("No dispatch error was expected."));

        try
        {
            Assert.True(dispatcher.TryEnqueue(CreateInput(1)));
            Assert.True(entered.Wait(Timeout, CancellationToken.None));

            bool accepted = await Task.Run(() => dispatcher.TryEnqueue(CreateInput(2)), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);

            Assert.True(accepted);
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task TryEnqueue_WhenQueueIsFull_RejectsInsteadOfBlocking()
    {
        using var entered = new ManualResetEventSlim(initialState: false);
        using var release = new ManualResetEventSlim(initialState: false);
        using var dispatcher = new InputEventDispatcher(
            "test-dispatch",
            inputEvent =>
            {
                entered.Set();
                _ = release.Wait(Timeout, CancellationToken.None);
            },
            static _ => Assert.Fail("No dispatch error was expected."),
            capacity: 1);

        try
        {
            Assert.True(dispatcher.TryEnqueue(CreateInput(1)));
            Assert.True(entered.Wait(Timeout, CancellationToken.None));
            Assert.True(dispatcher.TryEnqueue(CreateInput(2)));

            bool accepted = await Task.Run(() => dispatcher.TryEnqueue(CreateInput(3)), CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);

            Assert.False(accepted);
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
        using var dispatcher = new InputEventDispatcher(
            "test-dispatch",
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
        Assert.True(secondEvent.Wait(Timeout, CancellationToken.None));
        Assert.Contains(errors, static error => error.Message is "subscriber failed");
    }

    [Fact]
    public void Dispose_StopsAcceptanceAndCompletesWorker()
    {
        var dispatcher = new InputEventDispatcher("test-dispatch", static _ => { }, static _ => { });

        dispatcher.Dispose();
        dispatcher.Dispose();

        Assert.True(dispatcher.IsCompleted);
        Assert.False(dispatcher.TryEnqueue(CreateInput(1)));
    }

    [Fact]
    public async Task Dispose_WhenSubscriberIsActive_WaitsForCallbackToFinish()
    {
        using var entered = new ManualResetEventSlim(initialState: false);
        using var release = new ManualResetEventSlim(initialState: false);
        var dispatcher = new InputEventDispatcher(
            "test-dispatch",
            inputEvent =>
            {
                entered.Set();
                _ = release.Wait(Timeout, CancellationToken.None);
            },
            static _ => { });
        Assert.True(dispatcher.TryEnqueue(CreateInput(1)));
        Assert.True(entered.Wait(Timeout, CancellationToken.None));

        Task disposeTask = Task.Run(dispatcher.Dispose, CancellationToken.None);
        Assert.False(dispatcher.IsCompleted);

        release.Set();
        await disposeTask.WaitAsync(Timeout, TimeProvider.System, CancellationToken.None);
        Assert.True(dispatcher.IsCompleted);
    }

    [Fact]
    public void Dispose_WhenCalledFromSubscriber_DoesNotDeadlock()
    {
        using var completed = new ManualResetEventSlim(initialState: false);
        InputEventDispatcher? dispatcherReference = null;
        var dispatcher = new InputEventDispatcher(
            "test-dispatch",
            _ =>
            {
                dispatcherReference!.Dispose();
                completed.Set();
            },
            static _ => { });
        dispatcherReference = dispatcher;

        Assert.True(dispatcher.TryEnqueue(CreateInput(1)));
        Assert.True(completed.Wait(Timeout, CancellationToken.None));
        Assert.True(SpinWait.SpinUntil(() => dispatcher.IsCompleted, Timeout));
    }

    private static CapturedInputEvent CreateInput(int value) => new()
    {
        Type = InputEventType.Key,
        Code = InputEventCode.KEY_A,
        Value = value,
    };
}
