using System.Threading.Tasks;
using Tmds.DBus;

namespace CrossMacro.Infrastructure.Wayland.DBus;

[DBusInterface("org.kde.kwin.Script")]
public interface IKWinScript : IDBusObject
{
    Task runAsync();
    Task stopAsync();
}
