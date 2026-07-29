
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KWinScriptingClient(DBusConnection connection) : LinuxDbusClientBase(connection, Service, Path, Interface)
{
    internal const string Service = "org.kde.KWin";
    internal const string Path = "/Scripting";
    internal const string Interface = "org.kde.kwin.Scripting";

    public Task<int> LoadScriptAsync(string filePath)
        => CallAsync("loadScript", ReadLoadScriptReply, "s", (ref MessageWriter writer) => writer.WriteString(filePath));

    public Task UnloadScriptAsync(string scriptName)
        => CallAsync("unloadScript", "s", (ref MessageWriter writer) => writer.WriteString(scriptName));

    internal static MessageBuffer CreateLoadScriptMessage(DBusConnection connection, string filePath)
    {
        var client = new KWinScriptingClient(connection);
        return client.CreateMethodCall("loadScript", "s", (ref MessageWriter writer) => writer.WriteString(filePath));
    }

    internal static int ReadLoadScriptReply(Message message, object? _)
        => message.GetBodyReader().ReadInt32();
}
