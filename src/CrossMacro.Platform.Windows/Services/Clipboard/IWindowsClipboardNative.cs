namespace CrossMacro.Platform.Windows.Services.Clipboard;

internal interface IWindowsClipboardNative
{
    public IntPtr Allocate(nuint byteCount);
    public IntPtr Lock(IntPtr handle);
    public bool Unlock(IntPtr handle);
    public IntPtr Free(IntPtr handle);
    public bool Open(IntPtr owner);
    public bool Empty();
    public bool Close();
    public IntPtr SetData(uint format, IntPtr handle);
}
