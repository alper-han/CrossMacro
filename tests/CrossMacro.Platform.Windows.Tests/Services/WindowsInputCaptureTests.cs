using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Windows.Native;
using CrossMacro.Platform.Windows.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Windows.Tests.Services;

public class WindowsInputCaptureTests
{
    [WindowsFact]
    public async Task StartAsync_WhenMouseHookInstallFails_ThrowsInvalidOperationException()
    {
        using var capture = new FailingWindowsInputCapture(failMouse: true, failKeyboard: false);
        capture.Configure(captureMouse: true, captureKeyboard: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => capture.StartAsync(CancellationToken.None));

        Assert.Contains("mouse hook", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [WindowsFact]
    public async Task StartAsync_WhenKeyboardHookInstallFails_ThrowsInvalidOperationException()
    {
        using var capture = new FailingWindowsInputCapture(failMouse: false, failKeyboard: true);
        capture.Configure(captureMouse: false, captureKeyboard: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => capture.StartAsync(CancellationToken.None));

        Assert.Contains("keyboard hook", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [WindowsFact]
    public async Task StartAsync_WhenCancelledDuringStartup_CancelsPromptly()
    {
        using var cts = new CancellationTokenSource();
        using var capture = new BlockingWindowsInputCapture();
        capture.Configure(captureMouse: true, captureKeyboard: false);

        var startTask = capture.StartAsync(cts.Token);
        await capture.HookInstallStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => startTask);
        capture.ReleaseHookInstall();
    }

    private sealed class FailingWindowsInputCapture : WindowsInputCapture
    {
        private readonly bool _failMouse;
        private readonly bool _failKeyboard;

        public FailingWindowsInputCapture(bool failMouse, bool failKeyboard)
        {
            _failMouse = failMouse;
            _failKeyboard = failKeyboard;
        }

        protected override IntPtr InstallMouseHook(IntPtr moduleHandle)
            => _failMouse ? IntPtr.Zero : base.InstallMouseHook(moduleHandle);

        protected override IntPtr InstallKeyboardHook(IntPtr moduleHandle)
            => _failKeyboard ? IntPtr.Zero : base.InstallKeyboardHook(moduleHandle);
    }

    private sealed class BlockingWindowsInputCapture : WindowsInputCapture
    {
        private readonly ManualResetEventSlim _releaseHookInstall = new(false);

        public TaskCompletionSource HookInstallStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override IntPtr InstallMouseHook(IntPtr moduleHandle)
        {
            HookInstallStarted.TrySetResult();
            _releaseHookInstall.Wait(TimeSpan.FromSeconds(2));
            return IntPtr.Zero;
        }

        public void ReleaseHookInstall() => _releaseHookInstall.Set();

        public new void Dispose()
        {
            _releaseHookInstall.Set();
            _releaseHookInstall.Dispose();
            base.Dispose();
        }
    }
}
