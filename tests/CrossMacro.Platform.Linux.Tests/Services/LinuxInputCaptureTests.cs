using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Linux.Native.Evdev;
using CrossMacro.Infrastructure.Linux.Native.UInput;
using CrossMacro.TestInfrastructure;

namespace CrossMacro.Platform.Linux.Tests.Services;

public class LinuxInputCaptureTests
{
    [LinuxFact]
    public async Task StartAsync_WhenNoMatchingDevicesFound_ShouldThrowInvalidOperationException()
    {
        using var capture = new LinuxInputCapture(
            () => Array.Empty<InputDeviceHelper.InputDevice>(),
            _ => new FakeLinuxInputReader());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.StartAsync(CancellationToken.None));

        Assert.Equal("No matching input devices found", exception.Message);
    }

    [LinuxFact]
    public async Task StartAsync_WhenAllReadersFailToOpen_ShouldThrowInvalidOperationException()
    {
        var devices = new[]
        {
            new InputDeviceHelper.InputDevice
            {
                Path = "/dev/input/event-test",
                Name = "Test Keyboard",
                IsKeyboard = true
            }
        };

        using var capture = new LinuxInputCapture(
            () => devices,
            _ => new FakeLinuxInputReader(startException: new InvalidOperationException("open failed")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.StartAsync(CancellationToken.None));

        Assert.Equal("Failed to open any input devices", exception.Message);
    }

    [LinuxFact]
    public async Task StartAsync_WhenAtLeastOneReaderStarts_ShouldRegisterCancellationStop()
    {
        var devices = new[]
        {
            new InputDeviceHelper.InputDevice
            {
                Path = "/dev/input/event-test",
                Name = "Test Mouse",
                IsMouse = true
            }
        };
        var reader = new FakeLinuxInputReader();

        using var capture = new LinuxInputCapture(
            () => devices,
            _ => reader);
        using var cts = new CancellationTokenSource();

        await capture.StartAsync(cts.Token);
        cts.Cancel();

        Assert.Equal(1, reader.StartCalls);
        Assert.Equal(1, reader.StopCalls);
    }

    [LinuxFact]
    public async Task StartAsync_WhenConfiguredForMouseOnly_FiltersKeyboardEventsFromCompositeDevice()
    {
        var devices = new[]
        {
            new InputDeviceHelper.InputDevice
            {
                Path = "/dev/input/event-test",
                Name = "Combo Device",
                IsMouse = true,
                IsKeyboard = true
            }
        };
        var reader = new FakeLinuxInputReader();

        using var capture = new LinuxInputCapture(() => devices, _ => reader);
        capture.Configure(captureMouse: true, captureKeyboard: false, captureGamepad: false);
        await capture.StartAsync(CancellationToken.None);

        InputCaptureEventArgs? received = null;
        capture.InputReceived += (_, args) => received = args;

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_KEY, code = 30, value = 1 });
        Assert.Null(received);

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_KEY, code = UInputNative.BTN_LEFT, value = 1 });
        Assert.NotNull(received);
        Assert.Equal(InputEventType.MouseButton, received!.Value.Type);
    }

    [LinuxFact]
    public async Task StartAsync_WhenConfiguredForKeyboardOnly_FiltersMouseEventsFromCompositeDevice()
    {
        var devices = new[]
        {
            new InputDeviceHelper.InputDevice
            {
                Path = "/dev/input/event-test",
                Name = "Combo Device",
                IsMouse = true,
                IsKeyboard = true
            }
        };
        var reader = new FakeLinuxInputReader();

        using var capture = new LinuxInputCapture(() => devices, _ => reader);
        capture.Configure(captureMouse: false, captureKeyboard: true, captureGamepad: false);
        await capture.StartAsync(CancellationToken.None);

        InputCaptureEventArgs? received = null;
        capture.InputReceived += (_, args) => received = args;

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_KEY, code = UInputNative.BTN_LEFT, value = 1 });
        Assert.Null(received);

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_KEY, code = 30, value = 1 });
        Assert.NotNull(received);
        Assert.Equal(InputEventType.Key, received!.Value.Type);
    }

    [LinuxFact]
    public async Task StartAsync_WhenConfiguredForMouseOnly_ForwardsAbsoluteMouseMoveEvents()
    {
        var devices = new[]
        {
            new InputDeviceHelper.InputDevice
            {
                Path = "/dev/input/event-test",
                Name = "Absolute Pointer",
                IsMouse = true
            }
        };
        var reader = new FakeLinuxInputReader();

        using var capture = new LinuxInputCapture(() => devices, _ => reader);
        capture.Configure(captureMouse: true, captureKeyboard: false, captureGamepad: false);
        await capture.StartAsync(CancellationToken.None);

        InputCaptureEventArgs? received = null;
        capture.InputReceived += (_, args) => received = args;

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_ABS, code = UInputNative.ABS_X, value = 512 });
        Assert.NotNull(received);
        Assert.Equal(InputEventType.MouseMove, received!.Value.Type);
        Assert.Equal(UInputNative.ABS_X, received.Value.Code);
        Assert.Equal(512, received.Value.Value);
    }

    [LinuxFact]
    public async Task StartAsync_WhenConfiguredForMouseOnly_ForwardsHorizontalWheelAsScrollEvent()
    {
        var devices = new[]
        {
            new InputDeviceHelper.InputDevice
            {
                Path = "/dev/input/event-test",
                Name = "Wheel Mouse",
                IsMouse = true
            }
        };
        var reader = new FakeLinuxInputReader();

        using var capture = new LinuxInputCapture(() => devices, _ => reader);
        capture.Configure(captureMouse: true, captureKeyboard: false, captureGamepad: false);
        await capture.StartAsync(CancellationToken.None);

        InputCaptureEventArgs? received = null;
        capture.InputReceived += (_, args) => received = args;

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_REL, code = UInputNative.REL_HWHEEL, value = 1 });

        Assert.NotNull(received);
        Assert.Equal(InputEventType.MouseScroll, received!.Value.Type);
        Assert.Equal(UInputNative.REL_HWHEEL, received.Value.Code);
    }

    [LinuxFact]
    public async Task StartAsync_WhenDeviceListContainsCrossMacroVirtualDevice_ShouldSkipIt()
    {
        var virtualFactoryCalls = 0;
        var devices = new[]
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
        };

        var realReader = new FakeLinuxInputReader(deviceName: "Real Keyboard");
        using var capture = new LinuxInputCapture(
            () => devices,
            device =>
            {
                if (device.Name == VirtualDeviceConstants.DeviceName)
                {
                    virtualFactoryCalls++;
                    return new FakeLinuxInputReader(deviceName: VirtualDeviceConstants.DeviceName);
                }

                return realReader;
            });

        await capture.StartAsync(CancellationToken.None);

        Assert.Equal(0, virtualFactoryCalls);
        Assert.Equal(1, realReader.StartCalls);
    }

    [LinuxFact]
    public async Task StartAsync_WhenDeviceOnlyMatchesCrossMacroName_ShouldNotTreatItAsOwnOutputDevice()
    {
        var reader = new FakeLinuxInputReader(deviceName: VirtualDeviceConstants.DeviceName);
        var devices = new[]
        {
            new InputDeviceHelper.InputDevice
            {
                Path = "/dev/input/event-renamed",
                Name = VirtualDeviceConstants.DeviceName,
                IsKeyboard = true,
                VendorId = 0x9999,
                ProductId = 0x8888
            }
        };

        using var capture = new LinuxInputCapture(() => devices, _ => reader);
        capture.Configure(captureMouse: false, captureKeyboard: true, captureGamepad: false);

        await capture.StartAsync(CancellationToken.None);

        Assert.Equal(1, reader.StartCalls);
    }

    [LinuxFact]
    public async Task StartAsync_WhenDeviceListContainsThirdPartyVirtualKeyboard_ShouldCaptureIt()
    {
        var virtualKeyboardReader = new FakeLinuxInputReader(deviceName: "gsr-ui virtual keyboard");
        var devices = new[]
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
        };

        using var capture = new LinuxInputCapture(() => devices, _ => virtualKeyboardReader);
        capture.Configure(captureMouse: false, captureKeyboard: true, captureGamepad: false);

        await capture.StartAsync(CancellationToken.None);

        InputCaptureEventArgs? received = null;
        capture.InputReceived += (_, args) => received = args;

        virtualKeyboardReader.Emit(new UInputNative.input_event { type = UInputNative.EV_KEY, code = 30, value = 1 });

        Assert.Equal(1, virtualKeyboardReader.StartCalls);
        Assert.NotNull(received);
        Assert.Equal(InputEventType.Key, received!.Value.Type);
        Assert.Equal("gsr-ui virtual keyboard", received.Value.DeviceName);
    }

    private sealed class FakeLinuxInputReader : LinuxInputCapture.ILinuxInputReader
    {
        private readonly Exception? _startException;
        private readonly string _deviceName;
        private event Action<LinuxInputCapture.ILinuxInputReader, UInputNative.input_event>? EventReceivedInternal;

        public FakeLinuxInputReader(string deviceName = "Fake Reader", Exception? startException = null)
        {
            _deviceName = deviceName;
            _startException = startException;
        }

        public string DeviceName => _deviceName;
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }

        public event Action<LinuxInputCapture.ILinuxInputReader, UInputNative.input_event>? EventReceived
        {
            add => EventReceivedInternal += value;
            remove => EventReceivedInternal -= value;
        }

        public event Action<Exception>? ErrorOccurred
        {
            add { }
            remove { }
        }

        public void Start()
        {
            StartCalls++;
            if (_startException != null)
            {
                throw _startException;
            }
        }

        public void Stop()
        {
            StopCalls++;
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
