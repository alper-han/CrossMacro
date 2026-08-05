namespace CrossMacro.Daemon.Tests.Services;

public sealed class DaemonInputCapturePolicyTests
{
    [Fact]
    public void ShouldForwardEvent_RespectsMouseKeyboardSelection()
    {
        var mouseMove = new UInputNative.input_event
        {
            type = UInputNative.EV_REL,
            code = UInputNative.REL_X,
        };
        var keyboard = new UInputNative.input_event
        {
            type = UInputNative.EV_KEY,
            code = 30,
        };
        var button = new UInputNative.input_event
        {
            type = UInputNative.EV_KEY,
            code = UInputNative.BTN_LEFT,
        };

        Assert.True(DaemonInputCapturePolicy.ShouldForwardEvent(mouseMove, captureMouse: true, captureKeyboard: false));
        Assert.False(DaemonInputCapturePolicy.ShouldForwardEvent(mouseMove, captureMouse: false, captureKeyboard: true));
        Assert.True(DaemonInputCapturePolicy.ShouldForwardEvent(keyboard, captureMouse: false, captureKeyboard: true));
        Assert.False(DaemonInputCapturePolicy.ShouldForwardEvent(keyboard, captureMouse: true, captureKeyboard: false));
        Assert.True(DaemonInputCapturePolicy.ShouldForwardEvent(button, captureMouse: true, captureKeyboard: false));
        Assert.False(DaemonInputCapturePolicy.ShouldForwardEvent(button, captureMouse: false, captureKeyboard: true));
    }

    [Fact]
    public void ShouldCaptureDevice_ExcludesOnlyCrossMacroVirtualDeviceIdentity()
    {
        var ownDevice = new InputDeviceHelper.InputDevice
        {
            Name = VirtualDeviceConstants.DeviceName,
            VendorId = VirtualDeviceConstants.VendorId,
            ProductId = VirtualDeviceConstants.ProductId,
            IsKeyboard = true,
        };
        var renamedDevice = new InputDeviceHelper.InputDevice
        {
            Name = VirtualDeviceConstants.DeviceName,
            VendorId = 0x9999,
            ProductId = 0x8888,
            IsKeyboard = true,
        };

        Assert.False(DaemonInputCapturePolicy.ShouldCaptureDevice(ownDevice, captureMouse: false, captureKeyboard: true));
        Assert.True(DaemonInputCapturePolicy.ShouldCaptureDevice(renamedDevice, captureMouse: false, captureKeyboard: true));
    }

    [Fact]
    public void ReportAccumulator_EmitsOnlyForwardedEventsAndBoundaryInOrder()
    {
        var accumulator = new InputCaptureReportAccumulator(captureMouse: false, captureKeyboard: true);
        var keyboard = new UInputNative.input_event
        {
            type = UInputNative.EV_KEY,
            code = 30,
            value = 1,
        };
        var mouse = new UInputNative.input_event
        {
            type = UInputNative.EV_REL,
            code = UInputNative.REL_X,
            value = 4,
        };
        var boundary = new UInputNative.input_event
        {
            type = UInputNative.EV_SYN,
            code = UInputNative.SYN_REPORT,
        };

        Assert.False(accumulator.TryAppend(mouse, out var incomplete));
        Assert.Null(incomplete);
        Assert.False(accumulator.TryAppend(keyboard, out incomplete));
        Assert.Null(incomplete);
        Assert.True(accumulator.TryAppend(boundary, out var report));
        Assert.NotNull(report);
        Assert.Collection(
            report,
            inputEvent => Assert.Equal((UInputNative.EV_KEY, (ushort)30), (inputEvent.type, inputEvent.code)),
            inputEvent => Assert.Equal((UInputNative.EV_SYN, UInputNative.SYN_REPORT), (inputEvent.type, inputEvent.code)));
    }

    [Fact]
    public void ReportAccumulator_DropsEmptyBoundary()
    {
        var accumulator = new InputCaptureReportAccumulator(captureMouse: true, captureKeyboard: true);
        var boundary = new UInputNative.input_event
        {
            type = UInputNative.EV_SYN,
            code = UInputNative.SYN_REPORT,
        };

        Assert.False(accumulator.TryAppend(boundary, out var report));
        Assert.Null(report);
    }
}
