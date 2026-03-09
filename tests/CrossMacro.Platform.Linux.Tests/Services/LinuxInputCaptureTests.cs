using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Native.Evdev;
using CrossMacro.Platform.Linux.Native.UInput;
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
        capture.Configure(captureMouse: true, captureKeyboard: false);
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
        capture.Configure(captureMouse: false, captureKeyboard: true);
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
        capture.Configure(captureMouse: true, captureKeyboard: false);
        await capture.StartAsync(CancellationToken.None);

        InputCaptureEventArgs? received = null;
        capture.InputReceived += (_, args) => received = args;

        reader.Emit(new UInputNative.input_event { type = UInputNative.EV_ABS, code = UInputNative.ABS_X, value = 512 });
        Assert.NotNull(received);
        Assert.Equal(InputEventType.MouseMove, received!.Value.Type);
        Assert.Equal(UInputNative.ABS_X, received.Value.Code);
        Assert.Equal(512, received.Value.Value);
    }

    private sealed class FakeLinuxInputReader : LinuxInputCapture.ILinuxInputReader
    {
        private readonly Exception? _startException;
        private event Action<LinuxInputCapture.ILinuxInputReader, UInputNative.input_event>? EventReceivedInternal;

        public FakeLinuxInputReader(Exception? startException = null)
        {
            _startException = startException;
        }

        public string DeviceName => "Fake Reader";
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
