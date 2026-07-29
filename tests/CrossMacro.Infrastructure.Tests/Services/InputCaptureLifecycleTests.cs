namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class InputCaptureLifecycleTests
{
    [Fact]
    public async Task StartAsync_LinksCallerCancellationAndCleansUpCanceledStartup()
    {
        var capture = Substitute.For<IInputCapture>();
        var startupCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = capture.StartAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            var token = call.Arg<CancellationToken>();
            _ = token.Register(() => startupCanceled.TrySetResult(true));
            return startupCanceled.Task.WaitAsync(token);
        });

        using var cancellation = new CancellationTokenSource();
        var lifecycle = new InputCaptureLifecycle();
        var startTask = lifecycle.StartAsync(
            () => capture,
            captureMouse: false,
            captureKeyboard: true,
            (_, _) => { },
            (_, _) => { },
            _ => { },
            (_, _) => { },
            cancellation.Token);

        await cancellation.CancelAsync();

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(() => startTask);
        Assert.True(startupCanceled.Task.IsCompletedSuccessfully);
        capture.Received(1).StopCapture();
        capture.Received(1).Dispose();
        Assert.False(lifecycle.HasActiveResources);
    }

    [Fact]
    public void Cleanup_AttemptsStopAndDisposeWhenCancellationThrows()
    {
        var capture = Substitute.For<IInputCapture>();
        _ = capture.StartAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            _ = call.Arg<CancellationToken>().Register(static () => throw new InvalidOperationException("cancel failed"));
            return Task.CompletedTask;
        });
        capture.When(x => x.StopCapture()).Do(_ => throw new InvalidOperationException("stop failed"));

        var lifecycle = new InputCaptureLifecycle();
        lifecycle.Start(
            () => capture,
            captureMouse: false,
            captureKeyboard: true,
            (_, _) => { },
            (_, _) => { },
            _ => { },
            (_, _) => { });

        lifecycle.Cleanup(
            (_, _) => { },
            (_, _) => { },
            _ => { });

        capture.Received(1).StopCapture();
        capture.Received(1).Dispose();
        Assert.False(lifecycle.HasActiveResources);
    }

    [Fact]
    public async Task CleanupAsync_UsesAsyncDisposeWhenCaptureSupportsIt()
    {
        var capture = new AsyncDisposableCapture();
        var lifecycle = new InputCaptureLifecycle();

        lifecycle.Start(
            () => capture,
            captureMouse: false,
            captureKeyboard: true,
            (_, _) => { },
            (_, _) => { },
            _ => { },
            (_, _) => { });

        await lifecycle.CleanupAsync(
            (_, _) => { },
            (_, _) => { },
            _ => { });

        Assert.True(capture.DisposeAsyncCalled);
        Assert.False(capture.StopCaptureCalled);
        Assert.False(capture.DisposeCalled);
        Assert.False(lifecycle.HasActiveResources);
    }

    private sealed class AsyncDisposableCapture : IInputCapture, IAsyncDisposable
    {
        public string ProviderName => "Async disposable test capture";

        public bool IsSupported => true;

        public bool StopCaptureCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public bool DisposeAsyncCalled { get; private set; }

        public event EventHandler<CapturedInputEventArgs>? InputReceived;

        public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

        public void Configure(bool captureMouse, bool captureKeyboard)
        {
        }

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public void StopCapture()
        {
            StopCaptureCalled = true;
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }

        public ValueTask DisposeAsync()
        {
            DisposeAsyncCalled = true;
            return ValueTask.CompletedTask;
        }
    }
}
