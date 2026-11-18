namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Portal;

internal readonly record struct PortalCaptureDeadline(TimeProvider Clock, long StartedAt, TimeSpan Budget)
{
    public static PortalCaptureDeadline Start(TimeSpan budget, TimeProvider clock) => new(clock, clock.GetTimestamp(), budget);

    public bool TryGetRemaining(out TimeSpan remaining)
    {
        remaining = Budget - Clock.GetElapsedTime(StartedAt);
        if (remaining <= TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
            return false;
        }

        return true;
    }
}
