
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KWinScriptClient(DBusConnection connection, int scriptId) : LinuxDbusClientBase(
    connection,
    Service,
    $"/Scripting/Script{scriptId.ToString(CultureInfo.InvariantCulture)}",
    Interface)
{
    internal const string Service = "org.kde.KWin";
    internal const string Interface = "org.kde.kwin.Script";

    public Task RunAsync()
        => CallAsync("run");
}
