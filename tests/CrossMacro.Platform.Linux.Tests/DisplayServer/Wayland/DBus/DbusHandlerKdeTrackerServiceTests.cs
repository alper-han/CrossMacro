namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;


public sealed class DbusHandlerKdeTrackerServiceTests
{
    [Fact]
    public async Task UpdateMethods_ShouldInvokeProvidedCallbacks()
    {
        var lastPosition = (X: 0, Y: 0);
        var lastResolution = (Width: 0, Height: 0);
        var lastBounds = (X: 0, Y: 0, Width: 0, Height: 0);

        var service = new KdeTrackerService(
            (x, y) => lastPosition = (x, y),
            (w, h) => lastResolution = (w, h),
            onDesktopBoundsUpdate: (x, y, w, h) => lastBounds = (x, y, w, h));

        await service.UpdatePositionAsync(120, 240);
        await service.UpdateResolutionAsync(1920, 1080);
        await service.UpdateDesktopBoundsAsync(-1920, -200, 4480, 1640);

        Assert.Equal((120, 240), lastPosition);
        Assert.Equal((1920, 1080), lastResolution);
        Assert.Equal((-1920, -200, 4480, 1640), lastBounds);
    }

    [LinuxFact]
    public async Task TryDispatchMethod_ShouldHandlePositionAndResolutionUpdates()
    {
        var lastPosition = (X: 0, Y: 0);
        var lastResolution = (Width: 0, Height: 0);
        var lastBounds = (X: 0, Y: 0, Width: 0, Height: 0);
        var service = new KdeTrackerService(
            (x, y) => lastPosition = (x, y),
            (w, h) => lastResolution = (w, h),
            onDesktopBoundsUpdate: (x, y, w, h) => lastBounds = (x, y, w, h));
        var handler = new KdeTrackerServiceMethodHandler(service);

        var positionRequest = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(CombineInt32Body(120, 240));
        var resolutionRequest = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(CombineInt32Body(1920, 1080));
        var boundsRequest = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(
            CombineInt32Body(-1920, -200, 4480, 1640));
        var unknownRequest = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(CombineInt32Body(120, 240));

        Assert.Equal(
            KdeTrackerServiceMethodHandler.DispatchResult.Handled,
            await handler.TryDispatchMethodAsync(
                KdeTrackerService.TrackerInterface,
                KdeTrackerService.UpdatePositionMethod,
                "ii",
                positionRequest));
        Assert.Equal(
            KdeTrackerServiceMethodHandler.DispatchResult.Handled,
            await handler.TryDispatchMethodAsync(
                KdeTrackerService.TrackerInterface,
                KdeTrackerService.UpdateResolutionMethod,
                "ii",
                resolutionRequest));
        Assert.Equal(
            KdeTrackerServiceMethodHandler.DispatchResult.Handled,
            await handler.TryDispatchMethodAsync(
                KdeTrackerService.TrackerInterface,
                KdeTrackerService.UpdateDesktopBoundsMethod,
                "iiii",
                boundsRequest));
        Assert.Equal(
            KdeTrackerServiceMethodHandler.DispatchResult.UnknownMethod,
            await handler.TryDispatchMethodAsync(
                KdeTrackerService.TrackerInterface,
                "Unknown",
                "ii",
                unknownRequest));

        Assert.Equal((120, 240), lastPosition);
        Assert.Equal((1920, 1080), lastResolution);
        Assert.Equal((-1920, -200, 4480, 1640), lastBounds);
    }

    [Fact]
    public async Task TryDispatchMethod_ShouldRejectWrongInterfaceWithoutInvokingCallbacks()
    {
        var lastPosition = (X: 0, Y: 0);
        var service = new KdeTrackerService(
            (x, y) => lastPosition = (x, y),
            (_, _) => { });
        var handler = new KdeTrackerServiceMethodHandler(service);

        var wrongInterfaceRequest = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(CombineInt32Body(120, 240));

        Assert.Equal(
            KdeTrackerServiceMethodHandler.DispatchResult.UnknownMethod,
            await handler.TryDispatchMethodAsync(
                "wrong.iface",
                KdeTrackerService.UpdatePositionMethod,
                "ii",
                wrongInterfaceRequest));
        Assert.Equal((0, 0), lastPosition);
    }

    [Fact]
    public async Task TryDispatchMethod_ShouldRejectInvalidSignatureWithoutInvokingCallbacks()
    {
        var lastPosition = (X: 0, Y: 0);
        var service = new KdeTrackerService(
            (x, y) => lastPosition = (x, y),
            (_, _) => { });
        var handler = new KdeTrackerServiceMethodHandler(service);

        var invalidSignatureRequest = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(
            DbusWrapperProtocolTestHelpers.EncodeStringBody("oops"));

        Assert.Equal(
            KdeTrackerServiceMethodHandler.DispatchResult.InvalidArguments,
            await handler.TryDispatchMethodAsync(
                KdeTrackerService.TrackerInterface,
                KdeTrackerService.UpdatePositionMethod,
                "s",
                invalidSignatureRequest));
        Assert.Equal((0, 0), lastPosition);
    }

    private static byte[] CombineInt32Body(int first, int second)
    {
        var firstBytes = DbusWrapperProtocolTestHelpers.EncodeInt32Body(first);
        var secondBytes = DbusWrapperProtocolTestHelpers.EncodeInt32Body(second);
        var combined = new byte[firstBytes.Length + secondBytes.Length];
        Buffer.BlockCopy(firstBytes, 0, combined, 0, firstBytes.Length);
        Buffer.BlockCopy(secondBytes, 0, combined, firstBytes.Length, secondBytes.Length);
        return combined;
    }

    private static byte[] CombineInt32Body(int first, int second, int third, int fourth)
    {
        var firstPair = CombineInt32Body(first, second);
        var secondPair = CombineInt32Body(third, fourth);
        var combined = new byte[firstPair.Length + secondPair.Length];
        Buffer.BlockCopy(firstPair, 0, combined, 0, firstPair.Length);
        Buffer.BlockCopy(secondPair, 0, combined, firstPair.Length, secondPair.Length);
        return combined;
    }
}
