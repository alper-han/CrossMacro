namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

public class KdeTrackerServiceTests
{
    [Fact]
    public async Task UpdateMethods_ShouldInvokeProvidedCallbacks()
    {
        var lastPosition = (X: 0, Y: 0);
        var lastResolution = (Width: 0, Height: 0);

        var service = new KdeTrackerService(
            (x, y) => lastPosition = (x, y),
            (w, h) => lastResolution = (w, h));

        await service.UpdatePositionAsync(120, 240);
        await service.UpdateResolutionAsync(1920, 1080);

        Assert.Equal((120, 240), lastPosition);
        Assert.Equal((1920, 1080), lastResolution);
        Assert.Equal("/Tracker", service.ObjectPath.ToString());
    }
}
