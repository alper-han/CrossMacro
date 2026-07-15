namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

using System;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using CrossMacro.TestInfrastructure;

public class DbusHandlerKdeTrackerServiceTests
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
    }

    [LinuxFact]
    public void TryDispatchMethod_ShouldHandlePositionAndResolutionUpdates()
    {
        var lastPosition = (X: 0, Y: 0);
        var lastResolution = (Width: 0, Height: 0);
        var service = new KdeTrackerService(
            (x, y) => lastPosition = (x, y),
            (w, h) => lastResolution = (w, h));
        var handler = new KdeTrackerServiceMethodHandler(service);

        var positionRequest = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(CombineInt32Body(120, 240));
        var resolutionRequest = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(CombineInt32Body(1920, 1080));
        var unknownRequest = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(CombineInt32Body(120, 240));

        Assert.Equal(
            KdeTrackerServiceMethodHandler.DispatchResult.Handled,
            handler.TryDispatchMethod(
                KdeTrackerService.TrackerInterface,
                KdeTrackerService.UpdatePositionMethod,
                "ii",
                positionRequest));
        Assert.Equal(
            KdeTrackerServiceMethodHandler.DispatchResult.Handled,
            handler.TryDispatchMethod(
                KdeTrackerService.TrackerInterface,
                KdeTrackerService.UpdateResolutionMethod,
                "ii",
                resolutionRequest));
        Assert.Equal(
            KdeTrackerServiceMethodHandler.DispatchResult.UnknownMethod,
            handler.TryDispatchMethod(
                KdeTrackerService.TrackerInterface,
                "Unknown",
                "ii",
                unknownRequest));

        Assert.Equal((120, 240), lastPosition);
        Assert.Equal((1920, 1080), lastResolution);
    }

    [Fact]
    public void TryDispatchMethod_ShouldRejectWrongInterfaceWithoutInvokingCallbacks()
    {
        var lastPosition = (X: 0, Y: 0);
        var service = new KdeTrackerService(
            (x, y) => lastPosition = (x, y),
            (_, _) => { });
        var handler = new KdeTrackerServiceMethodHandler(service);

        var wrongInterfaceRequest = DbusWrapperProtocolTestHelpers.CreateBodyOnlyMessage(CombineInt32Body(120, 240));

        Assert.Equal(
            KdeTrackerServiceMethodHandler.DispatchResult.UnknownMethod,
            handler.TryDispatchMethod(
                "wrong.iface",
                KdeTrackerService.UpdatePositionMethod,
                "ii",
                wrongInterfaceRequest));
        Assert.Equal((0, 0), lastPosition);
    }

    [Fact]
    public void TryDispatchMethod_ShouldRejectInvalidSignatureWithoutInvokingCallbacks()
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
            handler.TryDispatchMethod(
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
}
