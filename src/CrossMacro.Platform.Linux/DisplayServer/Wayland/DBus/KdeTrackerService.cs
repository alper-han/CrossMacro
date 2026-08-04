
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

internal sealed class KdeTrackerService(
    Action<int, int> onPositionUpdate,
    Action<int, int> onResolutionUpdate,
    string customPath = KdeTrackerService.TrackerObjectPath,
    Action<int, int, int, int>? onDesktopBoundsUpdate = null)
{
    public const string TrackerServiceName = LinuxDbusTransportBoundary.TrackerServiceName;
    public const string TrackerObjectPath = "/io/github/alper_han/crossmacro/Tracker";
    public const string TrackerInterface = "io.github.alper_han.crossmacro.Tracker";
    public const string UpdatePositionMethod = "UpdatePosition";
    public const string UpdateResolutionMethod = "UpdateResolution";
    public const string UpdateDesktopBoundsMethod = "UpdateDesktopBounds";
    public const string ReportWindowDataMethod = "ReportWindowData";

    internal ObjectPath ObjectPath { get; } = new ObjectPath(customPath);
    private readonly Action<int, int> _onPositionUpdate = onPositionUpdate;
    private readonly Action<int, int> _onResolutionUpdate = onResolutionUpdate;
    private readonly Action<int, int, int, int> _onDesktopBoundsUpdate = onDesktopBoundsUpdate
        ?? ((_, _, width, height) => onResolutionUpdate(width, height));

    public event Action<string, string>? OnWindowDataReceived;

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

    public Task UpdateDesktopBoundsAsync(int x, int y, int width, int height)
    {
        _onDesktopBoundsUpdate(x, y, width, height);
        return Task.CompletedTask;
    }

    public Task ReportWindowDataAsync(string correlationId, string jsonData)
    {
        OnWindowDataReceived?.Invoke(correlationId, jsonData);
        return Task.CompletedTask;
    }
}
