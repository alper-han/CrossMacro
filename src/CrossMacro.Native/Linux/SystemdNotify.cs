using System.Runtime.InteropServices;

namespace CrossMacro.Native.Linux;

/// <summary>
/// Provides integration with systemd's sd_notify protocol for service status notification.
/// Used to signal service readiness and shutdown to systemd when running as a Type=notify service.
/// </summary>
public static partial class SystemdNotify
{
    private const string LibSystemd = "libsystemd.so.0";

    [LibraryImport(LibSystemd, EntryPoint = "sd_notify", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int SdNotify(int unsetEnvironment, string state);

    /// <summary>
    /// Signals to systemd that the service has started up and is ready to accept requests.
    /// Should be called after the daemon socket is bound and ready.
    /// </summary>
    public static void Ready()
    {
        try
        {
            SdNotify(0, "READY=1");
        }
        catch (DllNotFoundException)
        {
            // Not running under systemd or libsystemd not available - ignore
        }
    }

    /// <summary>
    /// Signals to systemd that the service is beginning its shutdown sequence.
    /// Should be called when the daemon receives a termination signal.
    /// </summary>
    public static void Stopping()
    {
        try
        {
            SdNotify(0, "STOPPING=1");
        }
        catch (DllNotFoundException)
        {
            // Not running under systemd or libsystemd not available - ignore
        }
    }

    /// <summary>
    /// Signals to systemd that the service is still alive (watchdog ping).
    /// Only useful if WatchdogSec is configured in the service file.
    /// </summary>
    public static void Watchdog()
    {
        try
        {
            SdNotify(0, "WATCHDOG=1");
        }
        catch (DllNotFoundException)
        {
            // Not running under systemd or libsystemd not available - ignore
        }
    }

    /// <summary>
    /// Updates the status message displayed by systemctl status.
    /// </summary>
    public static void Status(string status)
    {
        try
        {
            SdNotify(0, $"STATUS={status}");
        }
        catch (DllNotFoundException)
        {
            // Not running under systemd or libsystemd not available - ignore
        }
    }
}
