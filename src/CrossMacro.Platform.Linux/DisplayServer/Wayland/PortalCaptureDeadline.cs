namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct PortalCaptureDeadline(long StartedAt, TimeSpan Budget)
{
    public static PortalCaptureDeadline Start(TimeSpan budget) => new(Stopwatch.GetTimestamp(), budget);

    public bool IsExpired => !TryGetRemaining(out _);

    public bool TryGetRemaining(out TimeSpan remaining)
    {
        remaining = Budget - Stopwatch.GetElapsedTime(StartedAt);
        if (remaining <= TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
            return false;
        }

        return true;
    }
}
