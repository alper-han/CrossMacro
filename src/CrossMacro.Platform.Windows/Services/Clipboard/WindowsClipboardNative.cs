namespace CrossMacro.Platform.Windows.Services.Clipboard;

[SupportedOSPlatform("windows")]
internal sealed class WindowsClipboardNative : IWindowsClipboardNative
{
    internal static WindowsClipboardNative Instance { get; } = new();

    public IntPtr Allocate(nuint byteCount) => Kernel32.GlobalAlloc(Kernel32.GHND, byteCount);
    public IntPtr Lock(IntPtr handle) => Kernel32.GlobalLock(handle);
    public bool Unlock(IntPtr handle) => Kernel32.GlobalUnlock(handle);
    public IntPtr Free(IntPtr handle) => Kernel32.GlobalFree(handle);
    public bool Open(IntPtr owner) => User32.OpenClipboard(owner);
    public bool Empty() => User32.EmptyClipboard();
    public bool Close() => User32.CloseClipboard();
    public IntPtr SetData(uint format, IntPtr handle) => User32.SetClipboardData(format, handle);
}
