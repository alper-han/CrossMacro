namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal readonly record struct PipeWireCaptureTiming(TimeSpan Timeout, bool RequiresSettlingFrame)
{
    internal static TimeSpan ImmediateCaptureSettleTimeout { get; } = TimeSpan.FromMilliseconds(250);

    public static PipeWireCaptureTiming Create(bool streamActivationSupported, TimeSpan requestedTimeout) =>
        new(
            streamActivationSupported && requestedTimeout == TimeSpan.Zero
                ? ImmediateCaptureSettleTimeout
                : requestedTimeout,
            streamActivationSupported);
}
