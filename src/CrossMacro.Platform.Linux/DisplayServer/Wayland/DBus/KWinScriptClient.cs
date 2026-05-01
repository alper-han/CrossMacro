using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KWinScriptClient : LinuxDbusClientBase
{
    internal const string Service = "org.kde.KWin";
    internal const string Interface = "org.kde.kwin.Script";

    public KWinScriptClient(DBusConnection connection, string scriptId)
        : base(connection, Service, $"/Scripting/Script{scriptId}", Interface)
    {
    }

    public Task RunAsync()
        => CallAsync("run");

    public Task StopAsync()
        => CallAsync("stop");
}
