
namespace CrossMacro.Platform.Windows.Tests.Services;

public class WindowsInputCaptureTests
{
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
            .WaitAsync(TimeSpan.FromSeconds(2))
            ;

        cts.Cancel();
        hookInstaller.ReleaseHookInstall();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => startTask);
    }

    private sealed class FailingHookInstaller : IWindowsHookInstaller
    {
        private static readonly IntPtr SuccessfulHookHandle = new(1);

        private readonly bool _failMouse;
        private readonly bool _failKeyboard;

        public FailingHookInstaller(bool failMouse, bool failKeyboard)
        {
            _failMouse = failMouse;
            _failKeyboard = failKeyboard;
        }

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
            HookInstallStarted.TrySetResult();

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
