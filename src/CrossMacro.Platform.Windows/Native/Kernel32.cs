using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Windows.Native;

internal static class Kernel32
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern uint GetCurrentThreadId();
}
