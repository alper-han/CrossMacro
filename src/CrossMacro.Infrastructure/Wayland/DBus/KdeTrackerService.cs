using System;
using System.Threading.Tasks;
using Tmds.DBus;

namespace CrossMacro.Infrastructure.Wayland.DBus;

public class KdeTrackerService : IMouseTrackerService
{
    public ObjectPath ObjectPath => new ObjectPath("/Tracker");
    private readonly Action<int, int> _onPositionUpdate;
    private readonly Action<int, int> _onResolutionUpdate;

    public KdeTrackerService(Action<int, int> onPositionUpdate, Action<int, int> onResolutionUpdate)
    {
        _onPositionUpdate = onPositionUpdate;
        _onResolutionUpdate = onResolutionUpdate;
    }

    public Task UpdatePositionAsync(int x, int y)
    {
        _onPositionUpdate(x, y);
        return Task.CompletedTask;
    }

    public Task UpdateResolutionAsync(int width, int height)
    {
        _onResolutionUpdate(width, height);
        return Task.CompletedTask;
    }
}
