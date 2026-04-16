namespace CrossMacro.Daemon.Tests.Services;

using System;
using System.Collections.Generic;
using CrossMacro.Daemon.Services;
using CrossMacro.Platform.Linux.Native.Evdev;
using CrossMacro.Platform.Linux.Native.UInput;

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
        var result = manager.StartCapture(captureMouse: true, captureKeyboard: false, received.Add);

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
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, received.Add);

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
        var result = manager.StartCapture(captureMouse: true, captureKeyboard: false, received.Add);

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
                    IsKeyboard = true
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
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, received.Add);

        Assert.True(result.Success);
        Assert.Equal(1, result.StartedDeviceCount);
        Assert.Equal(0, virtualFactoryCalls);
        Assert.Equal(1, realReader.StartCalls);
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
