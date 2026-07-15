namespace CrossMacro.Daemon.Tests.Services;


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
                    IsKeyboard = true,
                },
            },
            _ => reader);

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: true, captureKeyboard: false, received.Add);

        Assert.True(result.Success);

        reader.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = 30, value = 1 });
        Assert.Empty(received);

        reader.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT, value = 1 });
        Assert.Single(received);
    }

    [Fact]
    public void StartCapture_WhenReaderStartFails_DisposesReaderImmediately()
    {
        var reader = new FakeLinuxCaptureReader { ThrowOnStart = true };
        var manager = new InputCaptureManager(
            () =>
            [
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-test",
                    Name = "Test Keyboard",
                    IsKeyboard = true,
                },
            ],
            _ => reader);

        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, _ => { });

        Assert.False(result.Success);
        Assert.Equal(1, reader.DisposeCalls);
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
                    IsKeyboard = true,
                },
            },
            _ => reader);

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, received.Add);

        Assert.True(result.Success);

        reader.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT, value = 1 });
        Assert.Empty(received);

        reader.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = 30, value = 1 });
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
                    IsMouse = true,
                },
            },
            _ => reader);

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: true, captureKeyboard: false, received.Add);

        Assert.True(result.Success);

        reader.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_ABS, code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.ABS_X, value = 1200 });
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
                    ProductId = VirtualDeviceConstants.ProductId,
                },
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-real",
                    Name = "Real Keyboard",
                    IsKeyboard = true,
                },
            },
            device =>
            {
                if (string.Equals(device.Name, VirtualDeviceConstants.DeviceName, StringComparison.Ordinal))
                {
                    virtualFactoryCalls++;
                    return new FakeLinuxCaptureReader();
                }

                return realReader;
            });

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, received.Add);

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
                    ProductId = 0x8888,
                },
            },
            _ => reader);

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, received.Add);

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
                    ProductId = 0x5eba,
                },
            },
            _ => virtualKeyboardReader);

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, received.Add);

        Assert.True(result.Success);
        Assert.Equal(1, result.StartedDeviceCount);
        Assert.Equal(1, virtualKeyboardReader.StartCalls);

        virtualKeyboardReader.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = 30, value = 1 });

        Assert.Single(received);
        Assert.Equal(30, received[0].code);
    }

    private sealed class FakeLinuxCaptureReader : InputCaptureManager.ILinuxCaptureReader
    {
        private event Action<InputCaptureManager.ILinuxCaptureReader, CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>? EventReceivedInternal;

        public int StartCalls { get; private set; }

        public bool ThrowOnStart { get; init; }

        public int DisposeCalls { get; private set; }

        public event Action<InputCaptureManager.ILinuxCaptureReader, CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>? EventReceived
        {
            add => EventReceivedInternal += value;
            remove => EventReceivedInternal -= value;
        }

        public void Start()
        {
            StartCalls++;
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("reader start failed");
            }
        }

        public void Dispose()
        {
            DisposeCalls++;
        }

        public void Emit(CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event inputEvent)
        {
            EventReceivedInternal?.Invoke(this, inputEvent);
        }
    }
}
