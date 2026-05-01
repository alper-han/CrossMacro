using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KdeTrackerClient : LinuxDbusClientBase
{
    internal const string Interface = "io.github.alper_han.crossmacro.Tracker";

    public KdeTrackerClient(DBusConnection connection)
        : base(connection, LinuxDbusTransportBoundary.TrackerServiceName, LinuxDbusTransportBoundary.TrackerObjectPath, Interface)
    {
    }

    public Task UpdatePositionAsync(int x, int y)
        => CallAsyncByRef("UpdatePosition", "ii", (ref MessageWriter writer) =>
        {
            writer.WriteInt32(x);
            writer.WriteInt32(y);
        });

    internal static MessageBuffer CreateUpdatePositionMessage(DBusConnection connection, int x, int y)
    {
        var client = new KdeTrackerClient(connection);
        return client.CreateMethodCallByRef("UpdatePosition", "ii", (ref MessageWriter writer) =>
        {
            writer.WriteInt32(x);
            writer.WriteInt32(y);
        });
    }

    public Task UpdateResolutionAsync(int width, int height)
        => CallAsyncByRef("UpdateResolution", "ii", (ref MessageWriter writer) =>
        {
            writer.WriteInt32(width);
            writer.WriteInt32(height);
        });

    internal static MessageBuffer CreateUpdateResolutionMessage(DBusConnection connection, int width, int height)
    {
        var client = new KdeTrackerClient(connection);
        return client.CreateMethodCallByRef("UpdateResolution", "ii", (ref MessageWriter writer) =>
        {
            writer.WriteInt32(width);
            writer.WriteInt32(height);
        });
    }
}
