namespace CrossMacro.Platform.Windows.Services.Runtime;

/// <summary>
/// Windows wakes timers only every 15.6 ms by default, so a recorded 8 ms gap between two mouse moves is
/// replayed as about 16 ms and a macro plays back at roughly half speed. Asking for 1 ms timers fixes the
/// pacing. The request applies to this process and lasts until it exits.
/// </summary>
internal static class WindowsTimerResolution
{
    private static int _raised;

    internal static void EnsureRaised()
    {
        if (Interlocked.Exchange(ref _raised, 1) is 0)
        {
            _ = Winmm.TimeBeginPeriod(1);
        }
    }
}
