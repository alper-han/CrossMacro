using System.Threading.Tasks;
using Tmds.DBus;

namespace CrossMacro.Infrastructure.Wayland.DBus;

[DBusInterface("org.kde.kwin.Scripting")]
public interface IKWinScripting : IDBusObject
{
    Task<int> loadScriptAsync(string filePath);
    Task unloadScriptAsync(string scriptName);
}
