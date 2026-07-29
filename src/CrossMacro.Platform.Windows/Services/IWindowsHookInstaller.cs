namespace CrossMacro.Platform.Windows.Services;

internal interface IWindowsHookInstaller
{
    public IntPtr InstallMouseHook(IntPtr moduleHandle, Native.User32.HookProc hookProc);
    public IntPtr InstallKeyboardHook(IntPtr moduleHandle, Native.User32.HookProc hookProc);
}
