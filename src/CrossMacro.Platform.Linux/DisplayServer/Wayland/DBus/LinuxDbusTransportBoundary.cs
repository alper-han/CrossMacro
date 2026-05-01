using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

/// <summary>
/// Linux-only boundary for the Protocol transport layer.
/// Keeps DBus service names, object paths, and session connection creation local to the Linux platform assembly.
/// </summary>
internal static class LinuxDbusTransportBoundary
{
    internal const string TrackerServiceName = "io.github.alper_han.crossmacro.Tracker";
    internal const string TrackerObjectPath = "/io/github/alper_han/crossmacro/Tracker";

    internal static DBusConnection CreateSessionConnection()
    {
        return new DBusConnection(DBusAddress.Session!);
    }
}
