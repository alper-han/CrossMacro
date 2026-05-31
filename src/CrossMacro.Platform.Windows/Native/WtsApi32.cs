using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Windows.Native;

internal static partial class WtsApi32
{
    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSRegisterSessionNotification(IntPtr hWnd, uint dwFlags);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);
}
