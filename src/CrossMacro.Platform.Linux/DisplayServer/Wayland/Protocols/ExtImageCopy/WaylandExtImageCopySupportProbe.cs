namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.ExtImageCopy;

public sealed class WaylandExtImageCopySupportProbe : IExtImageCopySupportProbe
{
    public static WaylandExtImageCopySupportProbe Instance { get; } = CreateDefault();

    private readonly Func<WaylandSupportResult> _probeSupport;

    internal WaylandExtImageCopySupportProbe(Func<WaylandSupportResult> probeSupport)
    {
        _probeSupport = probeSupport ?? throw new ArgumentNullException(nameof(probeSupport));
    }

    public WaylandSupportResult ProbeSupport()
    {
        try
        {
            return _probeSupport();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException or IOException or TimeoutException)
        {
            return WaylandSupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
    }

    private static WaylandExtImageCopySupportProbe CreateDefault()
    {
        var environment = LinuxEnvironmentVariables.CaptureCurrentSnapshot();
        return new WaylandExtImageCopySupportProbe(() => WaylandExtImageCopyRegistryProbe.Probe(environment));
    }
}
