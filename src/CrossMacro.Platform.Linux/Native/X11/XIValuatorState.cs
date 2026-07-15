using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.X11;

[StructLayout(LayoutKind.Sequential)]
public struct XIValuatorState
{
    public int mask_len;
    public IntPtr mask;
}
