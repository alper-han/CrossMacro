namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal readonly record struct PipeWireCaptureTiming(TimeSpan Timeout)
{
    internal static TimeSpan ImmediateCaptureTimeout { get; } = TimeSpan.FromMilliseconds(250);
    internal static TimeSpan MaximumFrameTimeout { get; } = TimeSpan.FromSeconds(1);

    public static PipeWireCaptureTiming Create(TimeSpan requestedTimeout)
    {
        var timeout = requestedTimeout == TimeSpan.Zero
            ? ImmediateCaptureTimeout
            : requestedTimeout;
        return new PipeWireCaptureTiming(timeout > MaximumFrameTimeout ? MaximumFrameTimeout : timeout);
    }
}
