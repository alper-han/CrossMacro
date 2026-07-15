using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.Services;
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class WaylandExtImageCopySupportProbe : IExtImageCopySupportProbe
{
    public static WaylandExtImageCopySupportProbe Instance { get; } = CreateDefault();

    private readonly Func<ExtImageCopySupportResult> _probeSupport;

    internal WaylandExtImageCopySupportProbe(Func<ExtImageCopySupportResult> probeSupport)
    {
        _probeSupport = probeSupport ?? throw new ArgumentNullException(nameof(probeSupport));
    }

    public ExtImageCopySupportResult ProbeSupport()
    {
        try
        {
            return _probeSupport();
        }
        catch (DllNotFoundException ex)
        {
            return ExtImageCopySupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
        catch (EntryPointNotFoundException ex)
        {
            return ExtImageCopySupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ExtImageCopySupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
        catch (IOException ex)
        {
            return ExtImageCopySupportResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message);
        }
    }

    private static WaylandExtImageCopySupportProbe CreateDefault()
    {
        var environment = LinuxEnvironmentVariables.CaptureCurrentSnapshot();
        return new WaylandExtImageCopySupportProbe(() => WaylandExtImageCopyRegistryProbe.Probe(environment));
    }
}
