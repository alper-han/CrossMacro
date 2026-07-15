using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.X11;

[StructLayout(LayoutKind.Sequential)]
public struct XIEventMask
{
    public int DeviceId;
    public int MaskLen;
    public IntPtr Mask;
}
