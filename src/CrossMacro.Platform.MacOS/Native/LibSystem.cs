using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.MacOS.Native;

internal static class LibSystem
{
    private const string LibSystemLib = "/usr/lib/libSystem.dylib";

    [DllImport(LibSystemLib)]
    public static extern int pthread_main_np();
}

internal static class MacOSMainThread
{
    public static bool IsMainThread()
    {
        return !OperatingSystem.IsMacOS() || LibSystem.pthread_main_np() == 1;
    }
}
