using System;

namespace CrossMacro.Platform.MacOS.Native;

internal static class MacOSMainThread
{
    public static bool IsMainThread()
    {
        return !OperatingSystem.IsMacOS() || LibSystem.pthread_main_np() is 1;
    }
}
