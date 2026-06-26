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

    public Task<(string base64Data, int stride, bool hasAlpha)> CaptureAreaAsync(int x, int y, int width, int height)
        => CallAsync("CaptureArea", ReadCaptureAreaReply, "iiii", (ref MessageWriter writer) =>
        {
            writer.WriteInt32(x);
            writer.WriteInt32(y);
            writer.WriteInt32(width);
            writer.WriteInt32(height);
        });

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

    internal static (string base64Data, int stride, bool hasAlpha) ReadCaptureAreaReply(Message message, object? _)
    {
        var reader = message.GetBodyReader();
        return (reader.ReadString(), reader.ReadInt32(), reader.ReadBool());
    }
}
