
namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsInputCaptureTests
{
    [Fact]
    public void GetEvdevCode_WhenVirtualKeyIsNormalReturn_MapsToKeyEnter()
    {
        var evdevCode = CrossMacro.Platform.Windows.Helpers.WindowsKeyMap.GetEvdevCode(0x0D);

        Assert.Equal(InputEventCode.KEY_ENTER, evdevCode);
        Assert.NotEqual(InputEventCode.KEY_KPENTER, evdevCode);
    }

    [Fact]
    public void MapKeyboardEvent_WhenReturnIsExtended_MapsToKeypadEnter()
    {
        var evdevCode = WindowsInputCapture.MapKeyboardEvent(0x0D, 0x01);

        Assert.Equal(InputEventCode.KEY_KPENTER, evdevCode);
    }

    [Theory]
    [InlineData(User32.WM_XBUTTONDOWN, 1, InputEventCode.BTN_SIDE, 1)]
    [InlineData(User32.WM_XBUTTONUP, 1, InputEventCode.BTN_SIDE, 0)]
    [InlineData(User32.WM_XBUTTONDOWN, 2, InputEventCode.BTN_EXTRA, 1)]
    [InlineData(User32.WM_XBUTTONUP, 2, InputEventCode.BTN_EXTRA, 0)]
    public void TryMapMouseButtonOrScroll_WhenXButtonMessage_MapsButtonState(uint message, ushort xButton, ushort expectedCode, int expectedValue)
    {
        var mapped = WindowsInputCapture.TryMapMouseButtonOrScroll(message, (uint)xButton << 16, out var code, out var value, out var type);

        Assert.True(mapped);
        Assert.Equal(expectedCode, code);
        Assert.Equal(expectedValue, value);
        Assert.Equal(InputEventCode.EV_KEY, type);
    }

    [Fact]
    public void TryMapMouseButtonOrScroll_WhenXButtonIsUnknown_ReturnsFalse()
    {
        var mapped = WindowsInputCapture.TryMapMouseButtonOrScroll(User32.WM_XBUTTONDOWN, 3u << 16, out _, out _, out _);

        Assert.False(mapped);
    }

    [Theory]
    [InlineData(0xFF88u, -120)]
    [InlineData(0xFFFFu, -1)]
    public void TryMapMouseButtonOrScroll_WhenHorizontalWheelHasNegativeDelta_MapsSignedHorizontalScroll(uint encodedDelta, int expectedValue)
    {
        var mapped = WindowsInputCapture.TryMapMouseButtonOrScroll(User32.WM_MOUSEHWHEEL, encodedDelta << 16, out var code, out var value, out var type);

        Assert.True(mapped);
        Assert.Equal(InputEventCode.REL_HWHEEL, code);
        Assert.Equal(expectedValue, value);
        Assert.Equal(InputEventCode.EV_REL, type);
    }

    [Theory]
    [InlineData(0u, 0L, false)]
    [InlineData(0x10u, 0L, false)]
    [InlineData(0u, InputEventMarkers.TextExpansionKeyboardEvent, false)]
    [InlineData(0x10u, InputEventMarkers.TextExpansionKeyboardEvent, true)]
    [InlineData(0x12u, InputEventMarkers.TextExpansionKeyboardEvent, true)]
    public void ShouldIgnoreKeyboardHookEvent_RecognizesOnlyCrossMacroInjectedFlags(uint hookFlags, long extraInfo, bool expected)
    {
        Assert.Equal(expected, WindowsInputCapture.ShouldIgnoreKeyboardHookEvent(hookFlags, InputEventMarkers.ToIntPtr(extraInfo)));
    }

    [Theory]
    [InlineData(User32.WM_WTSSESSION_CHANGE, 0x8, true)]
    [InlineData(User32.WM_WTSSESSION_CHANGE, 0xF, true)]
    [InlineData(User32.WM_WTSSESSION_CHANGE, 0x7, false)]
    [InlineData(User32.WM_KEYDOWN, 0x8, false)]
    public void IsSessionRecoveryMessage_RecognizesUnlockAndDesktopReady(uint message, int reason, bool expected)
    {
        Assert.Equal(expected, WindowsInputCapture.IsSessionRecoveryMessage(message, new IntPtr(reason)));
    }

    [Theory]
    [InlineData(true, InputEventCode.ABS_X, 120, InputEventCode.ABS_Y, -30)]
    [InlineData(false, InputEventCode.REL_X, 20, InputEventCode.REL_Y, -10)]
    public void ResolveMouseMovement_UsesConfiguredCoordinateMode(
        bool useAbsoluteCoordinates,
        ushort expectedXCode,
        int expectedXValue,
        ushort expectedYCode,
        int expectedYValue)
    {
        var movement = WindowsInputCapture.ResolveMouseMovement(
            useAbsoluteCoordinates,
            currentX: 120,
            currentY: -30,
            previousX: 100,
            previousY: -20);

        Assert.Equal(expectedXCode, movement.XCode);
        Assert.Equal(expectedXValue, movement.XValue);
        Assert.Equal(expectedYCode, movement.YCode);
        Assert.Equal(expectedYValue, movement.YValue);
    }

    [Fact]
    public void ResolveMouseMovement_WhenDeltaOverflows_SaturatesRelativeCoordinates()
    {
        var movement = WindowsInputCapture.ResolveMouseMovement(
            useAbsoluteCoordinates: false,
            currentX: int.MaxValue,
            currentY: int.MinValue,
            previousX: int.MinValue,
            previousY: int.MaxValue);

        Assert.Equal(int.MaxValue, movement.XValue);
        Assert.Equal(int.MinValue, movement.YValue);
    }

    [Theory]
    [InlineData((ushort)0, 20, -10, true, 20, -10)]
    [InlineData((ushort)0, 0, 0, false, 0, 0)]
    [InlineData((ushort)1, 20, -10, false, 0, 0)]
    public void TryResolveRawRelativeMovement_UsesOnlyRelativeDeviceDeltas(
        ushort flags,
        int deltaX,
        int deltaY,
        bool expected,
        int expectedX,
        int expectedY)
    {
        bool resolved = WindowsInputCapture.TryResolveRawRelativeMovement(
            flags,
            deltaX,
            deltaY,
            out int actualX,
            out int actualY);

        Assert.Equal(expected, resolved);
        Assert.Equal(expectedX, actualX);
        Assert.Equal(expectedY, actualY);
    }

    [Fact]
    public void RawMouse_HasNativeLayout()
    {
        Assert.Equal(24, Marshal.SizeOf<RawMouse>());
    }

    [Theory]
    [InlineData(12_345_678L, 10_000_000L, 1_234_567L)]
    [InlineData(9_999_999L, 10_000_000L, 999_999L)]
    [InlineData(50_000_123L, 10_000_000L, 5_000_012L)]
    public void ToMicroseconds_ConvertsStopwatchTicksWithoutLosingWholeSeconds(
        long timestamp,
        long frequency,
        long expected)
    {
        Assert.Equal(expected, WindowsInputCapture.ToMicroseconds(timestamp, frequency));
    }

    [WindowsFact]
    public async Task StartAsync_WhenMouseHookInstallFails_ThrowsInvalidOperationException()
    {
        var hookInstaller = new FailingHookInstaller(failMouse: true, failKeyboard: false);
        using var capture = new WindowsInputCapture(hookInstaller);
        capture.Configure(captureMouse: true, captureKeyboard: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => capture.StartAsync(CancellationToken.None));

        Assert.Contains("mouse hook", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [WindowsFact]
    public async Task StartAsync_WhenKeyboardHookInstallFails_ThrowsInvalidOperationException()
    {
        var hookInstaller = new FailingHookInstaller(failMouse: false, failKeyboard: true);
        using var capture = new WindowsInputCapture(hookInstaller);
        capture.Configure(captureMouse: false, captureKeyboard: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => capture.StartAsync(CancellationToken.None));

        Assert.Contains("keyboard hook", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [WindowsFact]
    public async Task StartAsync_WhenCancelledDuringStartup_CancelsPromptly()
    {
        using var cts = new CancellationTokenSource();
        var hookInstaller = new BlockingHookInstaller();
        using var capture = new WindowsInputCapture(hookInstaller);
        capture.Configure(captureMouse: true, captureKeyboard: false);

        var startTask = capture.StartAsync(cts.Token);
        await hookInstaller.HookInstallStarted.Task
            .WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, cts.Token)
            ;

        await cts.CancelAsync();
        hookInstaller.ReleaseHookInstall();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => startTask);
    }

    private sealed class FailingHookInstaller(bool failMouse, bool failKeyboard) : IWindowsHookInstaller
    {
        private static readonly IntPtr SuccessfulHookHandle = new(1);

        private readonly bool _failMouse = failMouse;
        private readonly bool _failKeyboard = failKeyboard;

        public IntPtr InstallMouseHook(IntPtr moduleHandle, User32.HookProc hookProc)
            => _failMouse ? IntPtr.Zero : SuccessfulHookHandle;

        public IntPtr InstallKeyboardHook(IntPtr moduleHandle, User32.HookProc hookProc)
            => _failKeyboard ? IntPtr.Zero : SuccessfulHookHandle;
    }

    private sealed class BlockingHookInstaller : IWindowsHookInstaller
    {
        private readonly TaskCompletionSource _releaseHookInstall =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource HookInstallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IntPtr InstallMouseHook(IntPtr moduleHandle, User32.HookProc hookProc)
        {
            _ = HookInstallStarted.TrySetResult();

            if (!_releaseHookInstall.Task.Wait(TimeSpan.FromSeconds(2)))
            {
                throw new TimeoutException(
                    "Timed out waiting for the blocking hook-install fake to be released.");
            }

            return IntPtr.Zero;
        }

        public IntPtr InstallKeyboardHook(IntPtr moduleHandle, User32.HookProc hookProc)
            => throw new InvalidOperationException(
                "Keyboard hook installation was not expected during this test.");

        public void ReleaseHookInstall()
            => _releaseHookInstall.TrySetResult();
    }
}
