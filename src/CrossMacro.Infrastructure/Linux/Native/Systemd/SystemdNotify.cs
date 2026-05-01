using System.Runtime.InteropServices;

namespace CrossMacro.Infrastructure.Linux.Native.Systemd;

/// <summary>
/// Provides integration with systemd's sd_notify protocol for service status notification.
/// Used to signal service readiness and shutdown to systemd when running as a Type=notify service.
/// </summary>
public static partial class SystemdNotify
{
    private const string LibSystemd = "libsystemd.so.0";

    [LibraryImport(LibSystemd, EntryPoint = "sd_notify", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int SdNotify(int unsetEnvironment, string state);

    public static void Ready()
    {
        try
        {
            SdNotify(0, "READY=1");
        }
        catch (DllNotFoundException)
        {
        }
    }

    public static void Stopping()
    {
        try
        {
            SdNotify(0, "STOPPING=1");
        }
        catch (DllNotFoundException)
        {
        }
    }

    public static void Watchdog()
    {
        try
        {
            SdNotify(0, "WATCHDOG=1");
        }
        catch (DllNotFoundException)
        {
        }
    }

    public static void Status(string status)
    {
        try
        {
            SdNotify(0, $"STATUS={status}");
        }
        catch (DllNotFoundException)
        {
        }
    }
}
