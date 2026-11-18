
namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsInputCaptureTests
{
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
