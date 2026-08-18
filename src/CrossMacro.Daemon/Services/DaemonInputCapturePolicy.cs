namespace CrossMacro.Daemon.Services;

/// <summary>
/// Pure device and evdev filtering rules for daemon capture.
/// </summary>
internal static class DaemonInputCapturePolicy
{
    internal static bool ShouldForwardEvent(UInputNative.input_event inputEvent, bool captureMouse, bool captureKeyboard)
    {
        return inputEvent.type switch
        {
            UInputNative.EV_KEY when UInputNative.IsMouseButton(inputEvent.code) => captureMouse,
            UInputNative.EV_KEY => captureKeyboard,
            UInputNative.EV_REL => captureMouse,
            UInputNative.EV_ABS when inputEvent.code is UInputNative.ABS_X or UInputNative.ABS_Y => captureMouse,
            UInputNative.EV_SYN => captureMouse || captureKeyboard,
            _ => false,
        };
    }

    internal static bool IsReportBoundary(UInputNative.input_event inputEvent)
        => inputEvent.type == UInputNative.EV_SYN && inputEvent.code == UInputNative.SYN_REPORT;

    internal static bool ShouldCaptureDevice(
        InputDeviceHelper.InputDevice device,
        bool captureMouse,
        bool captureKeyboard)
    {
        if (VirtualDeviceConstants.IsCrossMacroVirtualDevice(device.Name, device.VendorId, device.ProductId))
        {
            return false;
        }

        return (captureMouse && device.IsMouse) || (captureKeyboard && device.IsKeyboard);
    }
}
