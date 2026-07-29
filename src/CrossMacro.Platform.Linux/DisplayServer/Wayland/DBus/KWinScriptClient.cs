
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KWinScriptClient(DBusConnection connection, string scriptId) : LinuxDbusClientBase(connection, Service, $"/Scripting/Script{scriptId}", Interface)
{
    internal const string Service = "org.kde.KWin";
    internal const string Interface = "org.kde.kwin.Script";

    public Task RunAsync()
        => CallAsync("run");

    public Task StopAsync()
        => CallAsync("stop");
}
