using System;
using System.Threading;
using System.Threading.Tasks;
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

    private sealed class FakeLinuxInputReader : LinuxInputCapture.ILinuxInputReader
    {
        private readonly Exception? _startException;

        public FakeLinuxInputReader(Exception? startException = null)
        {
            _startException = startException;
        }

        public string DeviceName => "Fake Reader";
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }

        public event Action<LinuxInputCapture.ILinuxInputReader, UInputNative.input_event>? EventReceived
        {
            add { }
            remove { }
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
    }
}
