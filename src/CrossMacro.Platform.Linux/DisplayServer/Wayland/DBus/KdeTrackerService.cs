using System;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KdeTrackerService
{
    public const string TrackerServiceName = LinuxDbusTransportBoundary.TrackerServiceName;
    public const string TrackerObjectPath = "/io/github/alper_han/crossmacro/Tracker";
    public const string TrackerInterface = "io.github.alper_han.crossmacro.Tracker";
    public const string UpdatePositionMethod = "UpdatePosition";
    public const string UpdateResolutionMethod = "UpdateResolution";

    internal ObjectPath ObjectPath => new ObjectPath(TrackerObjectPath);
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
