using System.Threading.Tasks;
using Tmds.DBus;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

[DBusInterface("io.github.alper_han.crossmacro.Tracker")]
public interface IMouseTrackerService : IDBusObject
{
    Task UpdatePositionAsync(int x, int y);
    Task UpdateResolutionAsync(int width, int height);
}
