namespace CrossMacro.Platform.Windows.Services.WindowManagement;

internal interface IWindowsWindowFocusNative
{
    public bool IsWindow(IntPtr window);
    public uint GetCurrentThreadId();
    public uint GetWindowThread(IntPtr window);
    public IntPtr GetForegroundWindow();
    public bool GetForegroundLockTimeout(out uint timeout);
    public bool SetForegroundLockTimeout(uint timeout);
    public bool AllowSetForegroundWindow();
    public bool UnlockForegroundWindow();
    public bool AttachThreadInput(uint currentThread, uint targetThread, bool fAttach);
    public bool IsIconic(IntPtr window);
    public bool RestoreWindow(IntPtr window);
    public bool BringWindowToTop(IntPtr window);
    public bool SetForegroundWindow(IntPtr window);
    public IntPtr SetActiveWindow(IntPtr window);
    public IntPtr SetFocus(IntPtr window);
}
