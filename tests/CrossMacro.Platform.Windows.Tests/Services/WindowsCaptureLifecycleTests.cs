namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsCaptureLifecycleTests
{
    [Fact]
    public async Task RepeatedStart_ReusesRunningSessionAndOriginalCancellationOwnership()
    {
        var session = new FakeSession();
        var creations = 0;
        using var capture = new WindowsInputCapture(() => { creations++; return session; });
        using var firstCancellation = new CancellationTokenSource();

        await capture.StartAsync(firstCancellation.Token);
        await capture.StartAsync(CancellationToken.None);

        Assert.Equal(1, creations);
        Assert.Equal(1, session.Starts);
        Assert.Equal(firstCancellation.Token, session.StartToken);
    }

    [Fact]
    public async Task Restart_WaitsForPreviousNativeSessionCompletion()
    {
        var first = new FakeSession();
        var second = new FakeSession();
        var creations = 0;
        using var capture = new WindowsInputCapture(() => ++creations is 1 ? first : second);
        capture.Configure(captureMouse: true, captureKeyboard: false);
        await capture.StartAsync(CancellationToken.None);
        capture.StopCapture();
        capture.Configure(captureMouse: false, captureKeyboard: true);

        var restart = capture.StartAsync(CancellationToken.None);
        Assert.False(restart.IsCompleted);
        Assert.Equal(1, creations);
        first.Complete();
        await restart.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);

        Assert.Equal(2, creations);
        Assert.Equal((false, true), second.Configuration);
        Assert.Equal(1, first.Disposals);
    }

    [Fact]
    public async Task CanceledAdditionalStart_DoesNotStopExistingSession()
    {
        var session = new FakeSession();
        using var capture = new WindowsInputCapture(() => session);
        await capture.StartAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.StartAsync(cancellation.Token));
        Assert.Equal(0, session.StopRequests);
    }

    [Fact]
    public async Task DisposeAsync_WaitsForNativeReleaseAndRejectsFutureStarts()
    {
        var session = new FakeSession();
        var capture = new WindowsInputCapture(() => session);
        await capture.StartAsync(CancellationToken.None);

        var disposal = capture.DisposeAsync().AsTask();
        Assert.False(disposal.IsCompleted);
        _ = await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.StartAsync(CancellationToken.None));
        session.Complete();
        await disposal.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
        Assert.Equal(1, session.Disposals);
    }

    [Fact]
    public async Task Dispose_SuppressesCallbacksFromSessionStillStopping()
    {
        var session = new FakeSession();
        var capture = new WindowsInputCapture(() => session);
        var events = 0;
        var errors = 0;
        capture.InputReceived += (_, _) => events++;
        capture.CaptureError += (_, _) => errors++;
        await capture.StartAsync(CancellationToken.None);
        session.Emit(new CapturedInputEventArgs(new CapturedInputEvent()));
        session.EmitError(new InputCaptureErrorEventArgs("before"));
#pragma warning disable CA1849, S6966 // Exercise the non-blocking synchronous disposal contract before native completion.
        capture.Dispose();
#pragma warning restore CA1849, S6966
        session.Emit(new CapturedInputEventArgs(new CapturedInputEvent()));
        session.EmitError(new InputCaptureErrorEventArgs("after"));
        Assert.Equal(1, events);
        Assert.Equal(1, errors);
        session.Complete();
        await capture.DisposeAsync();
    }

    [Fact]
    public async Task StopDuringStartup_RestartWaitsForOldSessionCleanup()
    {
        var oldStartup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = new FakeSession { Startup = oldStartup.Task };
        var second = new FakeSession();
        var creations = 0;
        using var capture = new WindowsInputCapture(() => ++creations is 1 ? first : second);
        var starting = capture.StartAsync(CancellationToken.None);
        Assert.False(starting.IsCompleted);
        capture.StopCapture();
        oldStartup.SetCanceled(CancellationToken.None);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting);
        var restarting = capture.StartAsync(CancellationToken.None);
        Assert.False(restarting.IsCompleted);
        Assert.Equal(1, creations);
        first.Complete();
        await restarting.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
        Assert.Equal(2, creations);
    }

    [Fact]
    public async Task SessionCancellationBeforeCompletion_RestartWaitsForNativeCleanup()
    {
        var first = new FakeSession();
        var second = new FakeSession();
        var creations = 0;
        using var capture = new WindowsInputCapture(() => ++creations is 1 ? first : second);
        await capture.StartAsync(CancellationToken.None);
        // Models the native session's own cancellation callback without a facade StopCapture call.
        first.StopCapture();
        var restarting = capture.StartAsync(CancellationToken.None);
        Assert.False(restarting.IsCompleted);
        Assert.Equal(1, creations);
        first.Complete();
        await restarting.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
        Assert.Equal(2, creations);
    }

    private sealed class FakeSession : IWindowsCaptureSession
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Completion => _completion.Task;
        public bool IsStopRequested => StopRequests > 0;
        public Task Startup { get; init; } = Task.CompletedTask;
        public string ProviderName => "fake";
        public bool IsSupported => true;
        public int Starts { get; private set; }
        public int StopRequests { get; private set; }
        public int Disposals { get; private set; }
        public CancellationToken StartToken { get; private set; }
        public (bool Mouse, bool Keyboard) Configuration { get; private set; }
        public event EventHandler<CapturedInputEventArgs>? InputReceived;
        public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;
        public void Configure(bool captureMouse, bool captureKeyboard) => Configuration = (captureMouse, captureKeyboard);
        public void ConfigureCoordinateMode(bool useAbsoluteCoordinates, bool useLogicalCoordinates) { }
        public Task StartAsync(CancellationToken ct)
        {
            Starts++;
            StartToken = ct;
            return Startup;
        }
        public void StopCapture() => StopRequests++;
        public void Dispose() => Disposals++;
        internal void Complete() => _completion.SetResult();
        internal void Emit(CapturedInputEventArgs args) => InputReceived?.Invoke(this, args);
        internal void EmitError(InputCaptureErrorEventArgs args) => CaptureError?.Invoke(this, args);
    }
}
