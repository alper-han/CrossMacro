namespace CrossMacro.Platform.Windows.Services;

internal sealed class DefaultWindowsHookInstaller : IWindowsHookInstaller
{
    public IntPtr InstallMouseHook(IntPtr moduleHandle, User32.HookProc hookProc)
        => User32.SetWindowsHookEx(User32.WH_MOUSE_LL, hookProc, moduleHandle, 0);

    public IntPtr InstallKeyboardHook(IntPtr moduleHandle, User32.HookProc hookProc)
        => User32.SetWindowsHookEx(User32.WH_KEYBOARD_LL, hookProc, moduleHandle, 0);
}
