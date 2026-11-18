
namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSInputCaptureTests
{
    [Fact]
    public void SystemDefinedConstants_MatchNativeGoldenValues()
    {
        Assert.Equal(14, (int)CoreGraphics.CGEventType.SystemDefined);
        Assert.Equal(8, MacOSSystemKeyMap.NxSubtypeAuxControlButtons);
        Assert.Equal(83, (int)CoreGraphics.CGEventField.EventSubtype);
        Assert.Equal(149, (int)CoreGraphics.CGEventField.EventData1);
        Assert.Equal(150, (int)CoreGraphics.CGEventField.EventData2);
    }

    [NonMacOSFact]
    public async Task StartAsync_WhenPlatformUnsupported_DoesNotRequestListenEventAccess()
    {
        var listenRequests = 0;
        using var capture = new MacOSInputCapture(() =>
        {
            listenRequests++;
            return true;
        });

        await capture.StartAsync(CancellationToken.None);

        Assert.Equal(0, listenRequests);
    }

    [MacOSFact]
    public void IsSupported_OnMacOS_ShouldBeTrue()
    {
        using var capture = new MacOSInputCapture(() => true);

        Assert.True(capture.IsSupported);
    }

    [MacOSFact]
    public async Task StartAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        using var capture = new MacOSInputCapture(() => true);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => capture.StartAsync(cts.Token));
    }

    [NonMacOSFact]
    public async Task StartAsync_OnNonMacOS_ShouldReturnWithoutThrowingAndRaiseError()
    {
        using var capture = new MacOSInputCapture(() => true);
        string? error = null;
        capture.CaptureError += (_, message) => error = message.Message;

        var exception = await Record.ExceptionAsync(() => capture.StartAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.NotNull(error);
        Assert.Contains("only supported on macOS", error, StringComparison.OrdinalIgnoreCase);
    }

    [NonMacOSFact]
    public async Task StartAsync_CalledMultipleTimesOnNonMacOS_ShouldNotThrow()
    {
        using var capture = new MacOSInputCapture(() => true);

        await capture.StartAsync(CancellationToken.None);
        var exception = await Record.ExceptionAsync(() => capture.StartAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task StartAsync_WhenMainRunLoopSourceCannotBeCreated_FailsAndReleasesTap()
    {
        using var native = new FakeInputCaptureNative { FailMainRunLoopSource = true };
        using var capture = new MacOSInputCapture(() => true, native, isMacOS: () => true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => capture.StartAsync(CancellationToken.None));

        Assert.Contains("run-loop source", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(FakeInputCaptureNative.MainTapHandle, native.ReleasedHandles);
    }

    [Fact]
    public async Task StopCapture_DisablesTapsBeforeCaptureThreadReleasesThem()
    {
        using var native = new FakeInputCaptureNative();
        using var capture = new MacOSInputCapture(() => true, native, isMacOS: () => true);
        await capture.StartAsync(CancellationToken.None);

        capture.StopCapture();

        Assert.True(native.RunLoopExited.Wait(TimeSpan.FromSeconds(2), CancellationToken.None));
        Assert.DoesNotContain(native.EnableAfterReleaseAttempts, static attempt => attempt);
        Assert.Contains((FakeInputCaptureNative.MainTapHandle, false), native.EnableCalls);
        Assert.Contains(FakeInputCaptureNative.MainTapHandle, native.ReleasedHandles);
    }

    [Fact]
    public async Task StopCapture_WhenRequestedBeforeRunLoopEntry_DoesNotStartAnUnboundedRunLoop()
    {
        using var beforeRunLoop = new ManualResetEventSlim(initialState: false);
        using var allowRunLoop = new ManualResetEventSlim(initialState: false);
        using var native = new FakeInputCaptureNative();
        using var capture = new MacOSInputCapture(
            () => true,
            native,
            isMacOS: () => true,
            beforeRunLoop: () =>
            {
                beforeRunLoop.Set();
                _ = allowRunLoop.Wait(TimeSpan.FromSeconds(2), CancellationToken.None);
            });
        await capture.StartAsync(CancellationToken.None);
        Assert.True(beforeRunLoop.Wait(TimeSpan.FromSeconds(1), CancellationToken.None));

        capture.StopCapture();
        allowRunLoop.Set();

        Assert.True(native.RunLoopExited.Wait(TimeSpan.FromSeconds(2), CancellationToken.None));
        Assert.Equal(0, native.RunLoopCalls);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotReplaceOriginalCancellationOwnership()
    {
        using var native = new FakeInputCaptureNative();
        using var originalCancellation = new CancellationTokenSource();
        using var capture = new MacOSInputCapture(() => true, native, isMacOS: () => true);
        await capture.StartAsync(originalCancellation.Token);

        await capture.StartAsync(CancellationToken.None);
        await originalCancellation.CancelAsync();

        Assert.True(native.RunLoopExited.Wait(TimeSpan.FromSeconds(2), CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunningAndNewTokenIsCanceled_ThrowsWithoutStoppingCapture()
    {
        using var native = new FakeInputCaptureNative();
        using var capture = new MacOSInputCapture(() => true, native, isMacOS: () => true);
        await capture.StartAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => capture.StartAsync(cancellation.Token));

        Assert.False(native.RunLoopExited.IsSet);
    }

    private sealed class FakeInputCaptureNative : IMacOSInputCaptureNative, IDisposable
    {
        private static readonly IntPtr s_runLoopHandle = new(1);
        private static readonly IntPtr s_sessionTap = new(2);
        internal static readonly IntPtr MainTapHandle = new(3);
        private static readonly IntPtr s_sessionSource = new(4);
        private static readonly IntPtr s_mainSource = new(5);

        private readonly ManualResetEventSlim _stopRunLoop = new(initialState: false);
        private int _tapCreationCount;

        public bool FailMainRunLoopSource { get; init; }
        public ManualResetEventSlim RunLoopExited { get; } = new(initialState: false);
        public int RunLoopCalls { get; private set; }
        public List<IntPtr> ReleasedHandles { get; } = [];
        public List<(IntPtr Tap, bool Enable)> EnableCalls { get; } = [];
        public List<bool> EnableAfterReleaseAttempts { get; } = [];

        public IntPtr GetCurrentRunLoop() => s_runLoopHandle;

        public IntPtr CreateEventTap(
            CoreGraphics.CGEventTapLocation location,
            CoreGraphics.CGEventTapPlacement placement,
            CoreGraphics.CGEventTapOptions options,
            ulong eventsOfInterest,
            IntPtr callback) =>
            Interlocked.Increment(ref _tapCreationCount) is 1 ? s_sessionTap : MainTapHandle;

        public IntPtr CreateRunLoopSource(IntPtr eventTap)
        {
            if (eventTap == s_sessionTap)
            {
                return s_sessionSource;
            }

            return FailMainRunLoopSource ? IntPtr.Zero : s_mainSource;
        }

        public void AddRunLoopSource(IntPtr runLoop, IntPtr source) { }

        public void EnableEventTap(IntPtr eventTap, bool enable)
        {
            EnableAfterReleaseAttempts.Add(ReleasedHandles.Contains(eventTap));
            EnableCalls.Add((eventTap, enable));
        }

        public void RunLoopOnce(double seconds)
        {
            RunLoopCalls++;
            _ = _stopRunLoop.Wait(TimeSpan.FromSeconds(seconds), CancellationToken.None);
        }

        public void StopRunLoop(IntPtr runLoop) => _stopRunLoop.Set();

        public void Release(IntPtr handle)
        {
            ReleasedHandles.Add(handle);
            if (handle == MainTapHandle)
            {
                RunLoopExited.Set();
            }
        }

        public void Dispose()
        {
            _stopRunLoop.Dispose();
            RunLoopExited.Dispose();
        }
    }
}
