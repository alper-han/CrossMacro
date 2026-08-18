namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxRequestAwareScreenFrameProviderTests
{
    [Fact]
    public async Task Dispose_WhenCaptureIsInFlight_CancelsCaptureBeforeDisposingTheBackend()
    {
        var backend = new BlockingScreenFrameProvider();
        var capabilityDetector = Substitute.For<ILinuxScreenReaderCapabilityDetector>();
        _ = capabilityDetector.GetSnapshot().Returns(new LinuxScreenReaderCapabilitySnapshot(
            Unavailable(LinuxScreenReaderBackend.KWinScreenShot2),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            Unavailable(LinuxScreenReaderBackend.WlrScreencopy),
            Unavailable(LinuxScreenReaderBackend.Portal)));
        _ = capabilityDetector.EnsureReadyAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var provider = new LinuxRequestAwareScreenFrameProvider(
            capabilityDetector,
            [LinuxScreenReaderBackend.ExtImageCopy],
            _ => backend,
            static _ => throw new InvalidOperationException("Wlr backend must not be created."),
            static _ => throw new InvalidOperationException("Portal backend must not be created."),
            static _ => throw new InvalidOperationException("KWin backend must not be created."),
            static _ => throw new InvalidOperationException("GNOME backend must not be created."));
        var captureTask = provider.CaptureFrameAsync(new ScreenRect(0, 0, 1, 1), ScreenReadOptions.Default);
        await backend.CaptureStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var disposeTask = Task.Factory.StartNew(
            provider.Dispose,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        try
        {
            var result = await captureTask;
            Assert.False(result.IsSuccess);
            Assert.Equal(ScreenReadErrorKind.Canceled, result.ErrorKind);
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(backend.IsDisposed);
        }
        finally
        {
            await ((Task)captureTask).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            await disposeTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            provider.Dispose();
        }
    }

    private static LinuxScreenReaderBackendCapability Unavailable(LinuxScreenReaderBackend backend) =>
        LinuxScreenReaderBackendCapability.Unavailable(backend, ScreenReadErrorKind.BackendUnavailable, "test unavailable");

    private sealed class BlockingScreenFrameProvider : IScreenFrameProvider
    {
        private readonly TaskCompletionSource<ScreenReadResult<ScreenFrame>> _captureCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CaptureStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string ProviderName => "blocking";

        public bool IsSupported => true;

        public bool IsDisposed { get; private set; }

        public async Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options)
        {
            CaptureStarted.TrySetResult();
            try
            {
                return await _captureCompletion.Task.WaitAsync(options.CancellationToken);
            }
            catch (OperationCanceledException) when (options.CancellationToken.IsCancellationRequested)
            {
                return ScreenReadResultFactory.Failure<ScreenFrame>(ScreenReadErrorKind.Canceled, "test capture canceled");
            }
        }

        public void Dispose() => IsDisposed = true;
    }
}
