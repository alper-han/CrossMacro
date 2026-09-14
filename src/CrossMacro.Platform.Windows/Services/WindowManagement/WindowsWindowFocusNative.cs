namespace CrossMacro.Platform.Windows.Services.WindowManagement;

internal sealed class WindowsWindowFocusNative : IWindowsWindowFocusNative
{
    public bool IsWindow(IntPtr window) => User32.IsWindow(window);
    public uint GetCurrentThreadId() => Kernel32.GetCurrentThreadId();
    public uint GetWindowThread(IntPtr window) => User32.GetWindowThreadProcessId(window, out _);
    public IntPtr GetForegroundWindow() => User32.GetForegroundWindow();
    public bool GetForegroundLockTimeout(out uint timeout)
    {
        timeout = 0;
        return User32.SystemParametersInfo(User32.SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref timeout, 0);
    }
    public bool SetForegroundLockTimeout(uint timeout) => User32.SystemParametersInfo(User32.SPI_SETFOREGROUNDLOCKTIMEOUT, 0, new IntPtr(timeout), 0);
    public bool AllowSetForegroundWindow() => User32.AllowSetForegroundWindow(User32.ASFW_ANY);
    public bool UnlockForegroundWindow() => User32.LockSetForegroundWindow(User32.LSFW_UNLOCK);
    public bool AttachThreadInput(uint currentThread, uint targetThread, bool fAttach) => User32.AttachThreadInput(currentThread, targetThread, fAttach);
    public bool IsIconic(IntPtr window) => User32.IsIconic(window);
    public bool RestoreWindow(IntPtr window) => User32.ShowWindow(window, User32.SW_RESTORE);
    public bool BringWindowToTop(IntPtr window) => User32.BringWindowToTop(window);
    public bool SetForegroundWindow(IntPtr window) => User32.SetForegroundWindow(window);
    public IntPtr SetActiveWindow(IntPtr window) => User32.SetActiveWindow(window);
    public IntPtr SetFocus(IntPtr window) => User32.SetFocus(window);
}
