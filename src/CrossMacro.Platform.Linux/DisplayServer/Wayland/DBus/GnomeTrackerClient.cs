using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class GnomeTrackerClient : LinuxDbusClientBase
{
    internal const string Interface = "io.github.alper_han.crossmacro.Tracker";

    public GnomeTrackerClient(DBusConnection connection)
        : base(connection, LinuxDbusTransportBoundary.TrackerServiceName, LinuxDbusTransportBoundary.TrackerObjectPath, Interface)
    {
    }

    public Task<(int x, int y)> GetPositionAsync()
        => CallAsync("GetPosition", ReadGetPositionReply);

    public Task<(int width, int height)> GetResolutionAsync()
        => CallAsync("GetResolution", ReadGetResolutionReply);

    internal static (int x, int y) ReadGetPositionReply(Message message, object? _)
    {
        var reader = message.GetBodyReader();
        return (reader.ReadInt32(), reader.ReadInt32());
    }

    internal static (int width, int height) ReadGetResolutionReply(Message message, object? _)
    {
        var reader = message.GetBodyReader();
        return (reader.ReadInt32(), reader.ReadInt32());
    }
}
