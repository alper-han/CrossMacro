
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class GnomeShellExtensionsClient(DBusConnection connection) : LinuxDbusClientBase(connection, Service, Path, Interface)
{
    internal const string Service = "org.gnome.Shell";
    internal const string Path = "/org/gnome/Shell";
    internal const string Interface = "org.gnome.Shell.Extensions";

    public Task<bool> EnableExtensionAsync(string uuid)
        => CallAsync("EnableExtension", ReadEnableExtensionReply, "s", (ref MessageWriter writer) => writer.WriteString(uuid));

    public Task<IDictionary<string, object>> GetExtensionInfoAsync(string uuid)
        => CallAsync("GetExtensionInfo", ReadGetExtensionInfoReply, "s", (ref MessageWriter writer) => writer.WriteString(uuid));

    internal static bool ReadEnableExtensionReply(Message message, object? _)
        => message.GetBodyReader().ReadBool();

    internal static IDictionary<string, object> ReadGetExtensionInfoReply(Message message, object? _)
    {
        var reader = message.GetBodyReader();
        var entries = new Dictionary<string, object>(StringComparer.Ordinal);

        var end = reader.ReadDictionaryStart();
        while (reader.HasNext(end))
        {
            var key = reader.ReadString();
            entries[key] = DBusVariantUnboxer.Unbox(reader.ReadVariantValue());
        }

        return entries;
    }
}
