namespace CrossMacro.Daemon.Tests.Services;

using System;
using System.Collections.Generic;
using CrossMacro.Daemon.Services;
using CrossMacro.Infrastructure.Linux.Native.Evdev;
using CrossMacro.Infrastructure.Linux.Native.UInput;

public class InputCaptureManagerTests
{
    [Fact]
    public void StopCapture_WhenNeverStarted_DoesNotThrow()
    {
        var manager = new InputCaptureManager();

        var ex = Record.Exception(manager.StopCapture);

        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_WhenNeverStarted_DoesNotThrow()
    {
        var manager = new InputCaptureManager();

        var ex = Record.Exception(manager.Dispose);

        Assert.Null(ex);
    }

    [Fact]
    public void StartCapture_WhenConfiguredForMouseOnly_FiltersKeyboardEventsFromCompositeDevice()
    {
        var reader = new FakeLinuxCaptureReader();
        var manager = new InputCaptureManager(
            () => new[]
            {
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-test",
                    Name = "Combo Device",
                    IsMouse = true,
                    IsKeyboard = true
                }
            },
            _ => reader);

        var received = new List<UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: true, captureKeyboard: false, captureGamepad: false, received.Add);

        Assert.True(result.Success);

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_KEY, code = 30, value = 1 });
        Assert.Empty(received);

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_KEY, code = UInputNative.BTN_LEFT, value = 1 });
        Assert.Single(received);
    }

    [Fact]
    public void StartCapture_WhenConfiguredForKeyboardOnly_FiltersMouseEventsFromCompositeDevice()
    {
        var reader = new FakeLinuxCaptureReader();
        var manager = new InputCaptureManager(
            () => new[]
            {
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-test",
                    Name = "Combo Device",
                    IsMouse = true,
                    IsKeyboard = true
                }
            },
            _ => reader);

        var received = new List<UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, captureGamepad: false, received.Add);

        Assert.True(result.Success);

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_KEY, code = UInputNative.BTN_LEFT, value = 1 });
        Assert.Empty(received);

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_KEY, code = 30, value = 1 });
        Assert.Single(received);
    }

    [Fact]
    public void StartCapture_WhenConfiguredForMouseOnly_ForwardsAbsoluteMouseMoveEvents()
    {
        var reader = new FakeLinuxCaptureReader();
        var manager = new InputCaptureManager(
            () => new[]
            {
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-test",
                    Name = "Absolute Pointer",
                    IsMouse = true
                }
            },
            _ => reader);

        var received = new List<UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: true, captureKeyboard: false, captureGamepad: false, received.Add);

        Assert.True(result.Success);

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_ABS, code = UInputNative.ABS_X, value = 1200 });
        Assert.Single(received);
    }

    [Fact]
    public void StartCapture_WhenDeviceListContainsCrossMacroVirtualDevice_ShouldSkipIt()
    {
        var virtualFactoryCalls = 0;
        var realReader = new FakeLinuxCaptureReader();
        var manager = new InputCaptureManager(
            () => new[]
            {
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-virtual",
                    Name = VirtualDeviceConstants.DeviceName,
                    IsKeyboard = true,
                    VendorId = VirtualDeviceConstants.VendorId,
                    ProductId = VirtualDeviceConstants.ProductId
                },
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-real",
                    Name = "Real Keyboard",
                    IsKeyboard = true
                }
            },
            device =>
            {
                if (device.Name == VirtualDeviceConstants.DeviceName)
                {
                    virtualFactoryCalls++;
                    return new FakeLinuxCaptureReader();
                }

                return realReader;
            });

        var received = new List<UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, captureGamepad: false, received.Add);

        Assert.True(result.Success);
        Assert.Equal(1, result.StartedDeviceCount);
        Assert.Equal(0, virtualFactoryCalls);
        Assert.Equal(1, realReader.StartCalls);
    }

    [Fact]
    public void StartCapture_WhenDeviceOnlyMatchesCrossMacroName_ShouldNotTreatItAsOwnOutputDevice()
    {
        var reader = new FakeLinuxCaptureReader();
        var manager = new InputCaptureManager(
            () => new[]
            {
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-renamed",
                    Name = VirtualDeviceConstants.DeviceName,
                    IsKeyboard = true,
                    VendorId = 0x9999,
                    ProductId = 0x8888
                }
            },
            _ => reader);

        var received = new List<UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, captureGamepad: false, received.Add);

        Assert.True(result.Success);
        Assert.Equal(1, result.StartedDeviceCount);
        Assert.Equal(1, reader.StartCalls);
    }

    [Fact]
    public void StartCapture_WhenDeviceListContainsThirdPartyVirtualKeyboard_ShouldCaptureIt()
    {
        var virtualKeyboardReader = new FakeLinuxCaptureReader();
        var manager = new InputCaptureManager(
            () => new[]
            {
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-gsr",
                    Name = "gsr-ui virtual keyboard",
                    IsVirtual = true,
                    IsKeyboard = true,
                    VendorId = 0xdec0,
                    ProductId = 0x5eba
                }
            },
            _ => virtualKeyboardReader);

        var received = new List<UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, captureGamepad: false, received.Add);

        Assert.True(result.Success);
        Assert.Equal(1, result.StartedDeviceCount);
        Assert.Equal(1, virtualKeyboardReader.StartCalls);

        virtualKeyboardReader.Emit(new UInputNative.input_event { type = UInputNative.EV_KEY, code = 30, value = 1 });

        Assert.Single(received);
        Assert.Equal(30, received[0].code);
    }

    private sealed class FakeLinuxCaptureReader : InputCaptureManager.ILinuxCaptureReader
    {
        private event Action<InputCaptureManager.ILinuxCaptureReader, UInputNative.input_event>? EventReceivedInternal;

        public int StartCalls { get; private set; }

        public event Action<InputCaptureManager.ILinuxCaptureReader, UInputNative.input_event>? EventReceived
        {
            add => EventReceivedInternal += value;
            remove => EventReceivedInternal -= value;
        }

        public void Start()
        {
            StartCalls++;
        }

        public void Dispose()
        {
        }

        public void Emit(UInputNative.input_event inputEvent)
        {
            EventReceivedInternal?.Invoke(this, inputEvent);
        }
    }
}
