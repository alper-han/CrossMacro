using System.Threading.Tasks;
using Tmds.DBus;

namespace CrossMacro.Infrastructure.Wayland.DBus;

using Tmds.DBus;

[DBusInterface("org.crossmacro.Tracker")]
public interface IMouseTrackerService : IDBusObject
{
    Task UpdatePositionAsync(int x, int y);
    Task UpdateResolutionAsync(int width, int height);
}
