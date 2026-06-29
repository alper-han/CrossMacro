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

    public Task<(int x, int y)> GetPositionAsync() => CallAsync("GetPosition", ReadGetPositionReply);
    public Task<(int width, int height)> GetResolutionAsync() => CallAsync("GetResolution", ReadGetResolutionReply);
    public Task<(string base64Data, int stride, bool hasAlpha)> CaptureAreaAsync(int x, int y, int width, int height)
        => CallAsync("CaptureArea", ReadCaptureAreaReply, "iiii", (ref MessageWriter writer) => { writer.WriteInt32(x); writer.WriteInt32(y); writer.WriteInt32(width); writer.WriteInt32(height); });

    // Window Management Methods
    public Task<string> GetWindowsAsync() => CallAsync("GetWindows", ReadStringReply);
    public Task<string> GetActiveWindowAsync() => CallAsync("GetActiveWindow", ReadStringReply);
    public Task<bool> FocusWindowAsync(string address) => CallAsync("FocusWindow", ReadBoolReply, "s", (ref MessageWriter w) => w.WriteString(address));
    public Task<bool> CloseWindowAsync(string address) => CallAsync("CloseWindow", ReadBoolReply, "s", (ref MessageWriter w) => w.WriteString(address));
    public Task<bool> MoveActiveWindowAsync(int x, int y) => CallAsync("MoveActiveWindow", ReadBoolReply, "ii", (ref MessageWriter w) => { w.WriteInt32(x); w.WriteInt32(y); });
    public Task<bool> ResizeActiveWindowAsync(int width, int height) => CallAsync("ResizeActiveWindow", ReadBoolReply, "ii", (ref MessageWriter w) => { w.WriteInt32(width); w.WriteInt32(height); });
    public Task<bool> FullscreenActiveWindowAsync() => CallAsync("FullscreenActiveWindow", ReadBoolReply);
    public Task<bool> MaximizeActiveWindowAsync() => CallAsync("MaximizeActiveWindow", ReadBoolReply);
    public Task<bool> CenterActiveWindowAsync() => CallAsync("CenterActiveWindow", ReadBoolReply);
    public Task<string> GetActiveWorkspaceAsync() => CallAsync("GetActiveWorkspace", ReadStringReply);
    public Task<bool> SwitchWorkspaceAsync(string name) => CallAsync("SwitchWorkspace", ReadBoolReply, "s", (ref MessageWriter w) => w.WriteString(name));
    public Task<bool> MoveActiveWindowToWorkspaceAsync(string name) => CallAsync("MoveActiveWindowToWorkspace", ReadBoolReply, "s", (ref MessageWriter w) => w.WriteString(name));
    public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string name) => CallAsync("MoveWindowToWorkspaceByAddress", ReadBoolReply, "ss", (ref MessageWriter w) => { w.WriteString(address); w.WriteString(name); });

    internal static (int x, int y) ReadGetPositionReply(Message message, object? _) { var r = message.GetBodyReader(); return (r.ReadInt32(), r.ReadInt32()); }
    internal static (int width, int height) ReadGetResolutionReply(Message message, object? _) { var r = message.GetBodyReader(); return (r.ReadInt32(), r.ReadInt32()); }
    internal static (string base64Data, int stride, bool hasAlpha) ReadCaptureAreaReply(Message message, object? _) { var r = message.GetBodyReader(); return (r.ReadString(), r.ReadInt32(), r.ReadBool()); }
    internal static string ReadStringReply(Message message, object? _) => message.GetBodyReader().ReadString();
    internal static bool ReadBoolReply(Message message, object? _) => message.GetBodyReader().ReadBool();
}
