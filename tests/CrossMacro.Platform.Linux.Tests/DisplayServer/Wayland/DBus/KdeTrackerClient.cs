namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

internal sealed class KdeTrackerClient : LinuxDbusClientBase
{
    private const string Interface = "io.github.alper_han.crossmacro.Tracker";

    public KdeTrackerClient(DBusConnection connection)
        : base(connection, LinuxDbusTransportBoundary.TrackerServiceName, LinuxDbusTransportBoundary.TrackerObjectPath, Interface)
    {
    }

    public Task UpdatePositionAsync(int x, int y)
        => CallAsyncByRefAsync("UpdatePosition", "ii", (ref MessageWriter writer) =>
        {
            writer.WriteInt32(x);
            writer.WriteInt32(y);
        });

    public Task UpdateResolutionAsync(int width, int height)
        => CallAsyncByRefAsync("UpdateResolution", "ii", (ref MessageWriter writer) =>
        {
            writer.WriteInt32(width);
            writer.WriteInt32(height);
        });
}
