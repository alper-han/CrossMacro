using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KdeKeyboardClient : LinuxDbusClientBase
{
    internal const string Service = "org.kde.keyboard";
    internal const string Path = "/Layouts";
    internal const string Interface = "org.kde.KeyboardLayouts";

    public KdeKeyboardClient(DBusConnection connection)
        : base(connection, Service, Path, Interface)
    {
    }

    public Task<uint> GetLayoutAsync()
        => CallAsync("getLayout", ReadGetLayoutReply);

    public Task<(string shortName, string variant, string displayName)[]> GetLayoutsListAsync()
        => CallAsync("getLayoutsList", ReadGetLayoutsListReply);

    internal static uint ReadGetLayoutReply(Message message, object? _)
        => message.GetBodyReader().ReadUInt32();

    internal static (string shortName, string variant, string displayName)[] ReadGetLayoutsListReply(Message message, object? _)
    {
        var reader = message.GetBodyReader();
        var layouts = new List<(string shortName, string variant, string displayName)>();
        var end = reader.ReadArrayStart(DBusType.Struct);

        while (reader.HasNext(end))
        {
            reader.AlignStruct();
            layouts.Add((reader.ReadString(), reader.ReadString(), reader.ReadString()));
        }

        return layouts.ToArray();
    }
}
