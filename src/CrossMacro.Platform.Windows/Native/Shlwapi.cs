using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Windows.Native;

internal static class Shlwapi
{
    [DllImport("shlwapi.dll", ExactSpelling = true)]
    public static extern IntPtr SHCreateMemStream(byte[] pInit, uint cbInit);
}