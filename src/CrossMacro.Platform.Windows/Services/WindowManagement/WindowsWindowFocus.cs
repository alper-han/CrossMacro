namespace CrossMacro.Platform.Windows.Services.WindowManagement;

/// <summary>Focus transaction, including restoration of temporary thread and timeout state.</summary>
internal sealed class WindowsWindowFocus(IWindowsWindowFocusNative native)
{
    private readonly IWindowsWindowFocusNative _native = native;

    public bool Focus(IntPtr hwnd)
    {
        if (!_native.IsWindow(hwnd))
        {
            return false;
        }

        var currentThread = _native.GetCurrentThreadId();
        var targetThread = _native.GetWindowThread(hwnd);
        var foreground = _native.GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : _native.GetWindowThread(foreground);
        var attachedToTarget = false;
        var attachedToForeground = false;
        var timeoutRead = _native.GetForegroundLockTimeout(out var oldTimeout);

        try
        {
            _ = _native.SetForegroundLockTimeout(0);
            _ = _native.AllowSetForegroundWindow();
            _ = _native.UnlockForegroundWindow();

            if (targetThread != 0 && targetThread != currentThread)
            {
                attachedToTarget = _native.AttachThreadInput(currentThread, targetThread, fAttach: true);
            }

            if (foregroundThread != 0 && foregroundThread != currentThread && foregroundThread != targetThread)
            {
                attachedToForeground = _native.AttachThreadInput(currentThread, foregroundThread, fAttach: true);
            }

            if (_native.IsIconic(hwnd))
            {
                _ = _native.RestoreWindow(hwnd);
            }

            _ = _native.BringWindowToTop(hwnd);
            var focused = _native.SetForegroundWindow(hwnd);
            _ = _native.SetActiveWindow(hwnd);
            _ = _native.SetFocus(hwnd);
            return focused || _native.GetForegroundWindow() == hwnd;
        }
        finally
        {
            if (attachedToForeground)
            {
                _ = _native.AttachThreadInput(currentThread, foregroundThread, fAttach: false);
            }

            if (attachedToTarget)
            {
                _ = _native.AttachThreadInput(currentThread, targetThread, fAttach: false);
            }

            if (timeoutRead)
            {
                _ = _native.SetForegroundLockTimeout(oldTimeout);
            }
        }
    }

}
