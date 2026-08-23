
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

    [Fact]
    public void ShouldIgnoreKeyboardEvent_RecognizesOnlyCrossMacroMarker()
    {
        Assert.True(MacOSInputCapture.ShouldIgnoreKeyboardEvent(InputEventMarkers.TextExpansionKeyboardEvent));
        Assert.False(MacOSInputCapture.ShouldIgnoreKeyboardEvent(0));
        Assert.False(MacOSInputCapture.ShouldIgnoreKeyboardEvent(123));
    }

    [Theory]
    [InlineData((uint)CoreGraphics.CGEventType.TapDisabledByTimeout, true)]
    [InlineData((uint)CoreGraphics.CGEventType.TapDisabledByUserInput, true)]
    [InlineData((uint)CoreGraphics.CGEventType.MouseMoved, false)]
    public void ShouldReenableEventTap_HandlesBothNativeDisableEvents(uint eventType, bool expected)
    {
        Assert.Equal(expected, MacOSInputCapture.ShouldReenableEventTap((CoreGraphics.CGEventType)eventType));
    }

    [Fact]
    public void GetCurrentTimestamp_UsesUnixMillisecondsScale()
    {
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        long timestamp = MacOSInputCapture.GetCurrentTimestamp();

        long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.InRange(timestamp, before, after);
    }

    [Fact]
    public void ToMicroseconds_ConvertsStopwatchTicksWithoutLosingTheFractionalSecond()
    {
        var microseconds = MacOSInputCapture.ToMicroseconds(timestamp: 12_345_678, frequency: 10_000_000);

        Assert.Equal(1_234_567, microseconds);
    }

    [Fact]
    public void ToMicroseconds_WhenFrequencyIsNotPositive_Throws()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => MacOSInputCapture.ToMicroseconds(timestamp: 1, frequency: 0));
    }

    [Theory]
    [InlineData(12_345_678UL, 12_345L)]
    [InlineData(1_000UL, 1L)]
    public void ResolveEventTimestampMicroseconds_UsesNativeQuartzNanoseconds(
        ulong nativeNanoseconds,
        long expectedMicroseconds)
    {
        Assert.Equal(
            expectedMicroseconds,
            MacOSInputCapture.ResolveEventTimestampMicroseconds(nativeNanoseconds, fallbackTimestampMicroseconds: 999));
    }

    [Fact]
    public void ResolveEventTimestampMicroseconds_WhenNativeTimestampIsZero_UsesFallback()
    {
        Assert.Equal(999, MacOSInputCapture.ResolveEventTimestampMicroseconds(0, fallbackTimestampMicroseconds: 999));
    }


    [Fact]
    public void TryCreateKeyboardInput_WhenNativeKeyIsUnknown_ReturnsNoMatchWithoutCodeZeroEvent()
    {
        bool created = MacOSInputCapture.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0xFFFF,
            default,
            timestamp: 123,
            out _);

        Assert.False(created);
    }

    [Fact]
    public void TryCreateKeyboardInput_WhenNativeKeyIsKnown_CreatesKeyEvent()
    {
        bool created = MacOSInputCapture.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0x00,
            default,
            timestamp: 123,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(InputEventCode.KEY_A, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
        Assert.Equal(123, inputEvent.Timestamp);
    }

    [Fact]
    public void TryCreateKeyboardInput_WithMonotonicTimestamp_PreservesBothTimestampRepresentations()
    {
        bool created = MacOSInputCapture.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0x00,
            default,
            timestamp: 123,
            timestampMicroseconds: 123_456,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(123, inputEvent.Timestamp);
        Assert.Equal(123_456, inputEvent.TimestampMicroseconds);
    }

    [Theory]
    [InlineData(0, InputEventCode.KEY_VOLUMEUP)]
    [InlineData(1, InputEventCode.KEY_VOLUMEDOWN)]
    [InlineData(2, InputEventCode.KEY_BRIGHTNESSUP)]
    [InlineData(3, InputEventCode.KEY_BRIGHTNESSDOWN)]
    [InlineData(7, InputEventCode.KEY_MUTE)]
    [InlineData(16, InputEventCode.KEY_PLAYPAUSE)]
    [InlineData(17, InputEventCode.KEY_NEXTSONG)]
    [InlineData(18, InputEventCode.KEY_PREVIOUSSONG)]
    [InlineData(19, InputEventCode.KEY_FASTFORWARD)]
    [InlineData(20, InputEventCode.KEY_REWIND)]
    public void TryCreateSystemDefinedInput_WhenSupportedMediaKeyIsPressed_CreatesKeyDownEvent(int keyType, int expectedCode)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0A),
            timestamp: 123,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
        Assert.Equal(123, inputEvent.Timestamp);
    }

    [Theory]
    [InlineData(0, InputEventCode.KEY_VOLUMEUP)]
    [InlineData(1, InputEventCode.KEY_VOLUMEDOWN)]
    [InlineData(2, InputEventCode.KEY_BRIGHTNESSUP)]
    [InlineData(3, InputEventCode.KEY_BRIGHTNESSDOWN)]
    [InlineData(7, InputEventCode.KEY_MUTE)]
    [InlineData(16, InputEventCode.KEY_PLAYPAUSE)]
    [InlineData(17, InputEventCode.KEY_NEXTSONG)]
    [InlineData(18, InputEventCode.KEY_PREVIOUSSONG)]
    [InlineData(19, InputEventCode.KEY_FASTFORWARD)]
    [InlineData(20, InputEventCode.KEY_REWIND)]
    public void TryCreateSystemDefinedInput_WhenSupportedMediaKeyIsReleased_CreatesKeyUpEvent(int keyType, int expectedCode)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0B),
            timestamp: 456,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.Key, inputEvent.Type);
        Assert.Equal(expectedCode, inputEvent.Code);
        Assert.Equal(0, inputEvent.Value);
        Assert.Equal(456, inputEvent.Timestamp);
    }

    [Fact]
    public void TryCreateSystemDefinedInput_WithMonotonicTimestamp_PreservesBothTimestampRepresentations()
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(16, 0x0A),
            timestamp: 456,
            timestampMicroseconds: 456_789,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(456, inputEvent.Timestamp);
        Assert.Equal(456_789, inputEvent.TimestampMicroseconds);
    }

    [Fact]
    public void CreateSystemDefinedData1_WhenPressed_UsesGoldenNativeEncoding()
    {
        Assert.Equal((16 << 16) | 0x0A00, CreateSystemDefinedData1(16, 0x0A));
    }

    [Fact]
    public void CreateSystemDefinedData1_WhenReleased_UsesGoldenNativeEncoding()
    {
        Assert.Equal((16 << 16) | 0x0B00, CreateSystemDefinedData1(16, 0x0B));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(14)]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(23)]
    public void TryCreateSystemDefinedInput_WhenKeyTypeIsUnsupported_ReturnsNoMatchWithoutCodeZeroEvent(int keyType)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0A),
            timestamp: 123,
            out var inputEvent);

        Assert.False(created);
        Assert.Equal(default, inputEvent);
    }

    [Fact]
    public void CreateHidEventMask_WhenSessionSystemDefinedTapIsUsed_ExcludesSystemDefined()
    {
        var mask = MacOSInputCapture.CreateHidEventMask(useSessionSystemDefinedTap: true);

        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.KeyDown));
        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.KeyUp));
        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.FlagsChanged));
        Assert.False(ContainsEvent(mask, CoreGraphics.CGEventType.SystemDefined));
    }

    [Fact]
    public void CreateHidEventMask_IncludesScrollWheelForBothAxisCapture()
    {
        var mask = MacOSInputCapture.CreateHidEventMask(useSessionSystemDefinedTap: true);

        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.ScrollWheel));
        Assert.Equal(12, (int)CoreGraphics.CGEventField.ScrollWheelEventDeltaAxis2);
    }

    [Theory]
    [InlineData(InputEventCode.REL_WHEEL, 2)]
    [InlineData(InputEventCode.REL_HWHEEL, -3)]
    public void TryCreateScrollInput_MapsBothScrollAxesAndPreservesMonotonicTimestamp(ushort code, long value)
    {
        bool created = MacOSInputCapture.TryCreateScrollInput(
            code,
            value,
            timestamp: 123,
            timestampMicroseconds: 123_456,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventType.MouseScroll, inputEvent.Type);
        Assert.Equal(code, inputEvent.Code);
        Assert.Equal(value, inputEvent.Value);
        Assert.Equal(123, inputEvent.Timestamp);
        Assert.Equal(123_456, inputEvent.TimestampMicroseconds);
    }

    [Fact]
    public void TryCreateScrollInput_WithZeroOrUnknownAxis_ReturnsNoEvent()
    {
        Assert.False(MacOSInputCapture.TryCreateScrollInput(InputEventCode.REL_WHEEL, 0, 1, 1, out _));
        Assert.False(MacOSInputCapture.TryCreateScrollInput(InputEventCode.REL_X, 1, 1, 1, out _));
    }

    [Theory]
    [InlineData(false, 3, 40, 65536, 3)]
    [InlineData(true, 3, 40, 65536, 40)]
    [InlineData(true, 0, 0, 32768, 1)]
    [InlineData(true, 0, 0, -32768, -1)]
    [InlineData(true, -2, 0, 0, -2)]
    public void ResolveScrollDelta_PreservesLineScrollAndContinuousTrackpadDirection(
        bool isContinuous,
        long lineDelta,
        long pointDelta,
        long fixedPointDelta,
        long expected)
    {
        Assert.Equal(
            expected,
            MacOSInputCapture.ResolveScrollDelta(isContinuous, lineDelta, pointDelta, fixedPointDelta));
    }

    [Fact]
    public void CreateHidEventMask_WhenSessionSystemDefinedTapIsUnavailable_IncludesSystemDefinedFallback()
    {
        var mask = MacOSInputCapture.CreateHidEventMask(useSessionSystemDefinedTap: false);

        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.SystemDefined));
    }

    [Fact]
    public void CreateHidEventMask_WhenSessionSystemDefinedSourceCreationFails_IncludesSystemDefinedFallback()
    {
        const bool useSessionSystemDefinedTap = false;

        var mask = MacOSInputCapture.CreateHidEventMask(useSessionSystemDefinedTap);

        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.SystemDefined));
    }

    [Fact]
    public void CreateSystemDefinedEventMask_IncludesOnlySystemDefined()
    {
        var mask = MacOSInputCapture.CreateSystemDefinedEventMask();

        Assert.True(ContainsEvent(mask, CoreGraphics.CGEventType.SystemDefined));
        Assert.False(ContainsEvent(mask, CoreGraphics.CGEventType.KeyDown));
        Assert.False(ContainsEvent(mask, CoreGraphics.CGEventType.MouseMoved));
    }

    [Fact]
    public void CreateObserveOnlyTapOptions_UsesListenOnly()
    {
        Assert.Equal(CoreGraphics.CGEventTapOptions.ListenOnly, MacOSInputCapture.CreateObserveOnlyTapOptions());
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

    [Theory]
    [InlineData(10, 8, 16, 0x0A)]
    [InlineData(14, 7, 16, 0x0A)]
    [InlineData(14, 8, 16, 0x09)]
    public void TryCreateSystemDefinedInput_WhenPayloadIsNotAuditedSubtype8PressOrRelease_ReturnsNoMatch(
        int eventType,
        long subtype,
        int keyType,
        int state)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            (CoreGraphics.CGEventType)eventType,
            subtype,
            CreateSystemDefinedData1(keyType, state),
            timestamp: 123,
            out _);

        Assert.False(created);
    }

    [Theory]
    [InlineData(0x65)]
    [InlineData(0x6D)]
    public void TryCreateSystemDefinedInput_WhenPayloadLooksLikeFunctionKeyVirtualKey_ReturnsNoMatch(int keyType)
    {
        bool created = MacOSInputCapture.TryCreateSystemDefinedInput(
            CoreGraphics.CGEventType.SystemDefined,
            subtype: 8,
            data1: CreateSystemDefinedData1(keyType, 0x0A),
            timestamp: 123,
            out _);

        Assert.False(created);
    }

    [Fact]
    public void TryCreateKeyboardInput_WhenOrdinaryFunctionKeyIsKnown_RemainsKeyMapBacked()
    {
        bool created = MacOSInputCapture.TryCreateKeyboardInput(
            CoreGraphics.CGEventType.KeyDown,
            0x65,
            default,
            timestamp: 123,
            out var inputEvent);

        Assert.True(created);
        Assert.Equal(InputEventCode.KEY_F9, inputEvent.Code);
        Assert.Equal(1, inputEvent.Value);
    }

    [Theory]
    [InlineData(2, MouseButtonCode.Middle)]
    [InlineData(3, MouseButtonCode.Side1)]
    [InlineData(4, MouseButtonCode.Side2)]
    public void TryMapOtherMouseButton_UsesCoreGraphicsButtonNumbers(long buttonNumber, int expectedButton)
    {
        bool mapped = MacOSInputCapture.TryMapOtherMouseButton(buttonNumber, out int button);

        Assert.True(mapped);
        Assert.Equal(expectedButton, button);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void TryMapOtherMouseButton_WhenUnsupported_ReturnsFalse(long buttonNumber)
    {
        Assert.False(MacOSInputCapture.TryMapOtherMouseButton(buttonNumber, out _));
    }

    private static long CreateSystemDefinedData1(int keyType, int state)
    {
        return (keyType << 16) | (state << 8);
    }

    private static bool ContainsEvent(ulong mask, CoreGraphics.CGEventType eventType)
    {
        return (mask & (1UL << (int)eventType)) != 0;
    }

    [MacOSFact]
    public void IsSupported_OnMacOS_ShouldBeTrue()
    {
        using var capture = new MacOSInputCapture();

        Assert.True(capture.IsSupported);
    }

    [MacOSFact]
    public async Task StartAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        using var capture = new MacOSInputCapture();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => capture.StartAsync(cts.Token));
    }

    [NonMacOSFact]
    public async Task StartAsync_OnNonMacOS_ShouldReturnWithoutThrowingAndRaiseError()
    {
        using var capture = new MacOSInputCapture();
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
        using var capture = new MacOSInputCapture();

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
