namespace CrossMacro.Daemon.Tests.Services;


public sealed class InputCaptureManagerTests
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
    public void StopCapture_WhenCalledRepeatedly_ShouldDisposeReaderOnce()
    {
        var reader = new FakeLinuxCaptureReader();
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

        Assert.True(result.Success);

        manager.StopCapture();
        manager.StopCapture();
        manager.Dispose();

        Assert.Equal(1, reader.DisposeCalls);
    }

    [Fact]
    public void StartCapture_WhenConfiguredForMouseOnly_FiltersKeyboardEventsFromCompositeDevice()
    {
        var reader = new FakeLinuxCaptureReader();
        var manager = new InputCaptureManager(
            () =>
            [
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-test",
                    Name = "Combo Device",
                    IsMouse = true,
                    IsKeyboard = true,
                },
            ],
            _ => reader);

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: true, captureKeyboard: false, received.Add);

        Assert.True(result.Success);

        reader.EmitReport(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = 30, value = 1 });
        Assert.Empty(received);

        reader.EmitReport(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT, value = 1 });
        Assert.Equal(2, received.Count);
        Assert.Equal(UInputNative.BTN_LEFT, received[0].code);
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
            () =>
            [
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-test",
                    Name = "Combo Device",
                    IsMouse = true,
                    IsKeyboard = true,
                },
            ],
            _ => reader);

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, received.Add);

        Assert.True(result.Success);

        reader.EmitReport(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.BTN_LEFT, value = 1 });
        Assert.Empty(received);

        reader.EmitReport(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = 30, value = 1 });
        Assert.Equal(2, received.Count);
        Assert.Equal((ushort)30, received[0].code);
    }

    [Fact]
    public void StartCapture_WhenConfiguredForKeyboardOnly_ForwardsKeyboardEventThenSyncInOrder()
    {
        var reader = new FakeLinuxCaptureReader();
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

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, received.Add);

        Assert.True(result.Success);

        reader.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
        {
            type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY,
            code = 30,
            value = 1,
        });
        Assert.Empty(received);
        reader.Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
        {
            type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_SYN,
            code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.SYN_REPORT,
            value = 0,
        });

        Assert.Collection(
            received,
            inputEvent =>
            {
                Assert.Equal(CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, inputEvent.type);
                Assert.Equal((ushort)30, inputEvent.code);
                Assert.Equal(1, inputEvent.value);
            },
            inputEvent =>
            {
                Assert.Equal(CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_SYN, inputEvent.type);
                Assert.Equal(CrossMacro.Platform.Linux.Native.UInput.UInputNative.SYN_REPORT, inputEvent.code);
                Assert.Equal(0, inputEvent.value);
            });
    }

    [Fact]
    public void StartCapture_WhenDeviceReportsOverlap_ForwardsEachReportAtomically()
    {
        var mouseReader = new FakeLinuxCaptureReader();
        var keyboardReader = new FakeLinuxCaptureReader();
        var manager = new InputCaptureManager(
            () =>
            [
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-mouse",
                    Name = "Mouse",
                    IsMouse = true,
                },
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-keyboard",
                    Name = "Keyboard",
                    IsKeyboard = true,
                },
            ],
            device => device.IsMouse ? mouseReader : keyboardReader);
        var received = new List<UInputNative.input_event>();

        var result = manager.StartCapture(captureMouse: true, captureKeyboard: true, received.Add);
        mouseReader.Emit(new UInputNative.input_event { type = UInputNative.EV_REL, code = UInputNative.REL_X, value = 4 });
        keyboardReader.EmitReport(new UInputNative.input_event { type = UInputNative.EV_KEY, code = 30, value = 1 });
        mouseReader.Emit(new UInputNative.input_event { type = UInputNative.EV_REL, code = UInputNative.REL_Y, value = 6 });
        mouseReader.Emit(new UInputNative.input_event { type = UInputNative.EV_SYN, code = UInputNative.SYN_REPORT });

        Assert.True(result.Success);
        Assert.Collection(
            received,
            inputEvent => Assert.Equal((UInputNative.EV_KEY, (ushort)30), (inputEvent.type, inputEvent.code)),
            inputEvent => Assert.Equal((UInputNative.EV_SYN, UInputNative.SYN_REPORT), (inputEvent.type, inputEvent.code)),
            inputEvent => Assert.Equal((UInputNative.EV_REL, UInputNative.REL_X), (inputEvent.type, inputEvent.code)),
            inputEvent => Assert.Equal((UInputNative.EV_REL, UInputNative.REL_Y), (inputEvent.type, inputEvent.code)),
            inputEvent => Assert.Equal((UInputNative.EV_SYN, UInputNative.SYN_REPORT), (inputEvent.type, inputEvent.code)));
    }

    [Fact]
    public void StartCapture_WhenConfiguredForMouseOnly_ForwardsAbsoluteMouseMoveEvents()
    {
        var reader = new FakeLinuxCaptureReader();
        var manager = new InputCaptureManager(
            () =>
            [
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-test",
                    Name = "Absolute Pointer",
                    IsMouse = true,
                },
            ],
            _ => reader);

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: true, captureKeyboard: false, received.Add);

        Assert.True(result.Success);

        reader.EmitReport(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_ABS, code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.ABS_X, value = 1200 });
        Assert.Equal(2, received.Count);
        Assert.Equal(UInputNative.ABS_X, received[0].code);
    }

    [Fact]
    public void StartCapture_WhenDeviceListContainsCrossMacroVirtualDevice_ShouldSkipIt()
    {
        var virtualFactoryCalls = 0;
        var realReader = new FakeLinuxCaptureReader();
        var manager = new InputCaptureManager(
            () =>
            [
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
            ],
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
            () =>
            [
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-renamed",
                    Name = VirtualDeviceConstants.DeviceName,
                    IsKeyboard = true,
                    VendorId = 0x9999,
                    ProductId = 0x8888,
                },
            ],
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
            () =>
            [
                new InputDeviceHelper.InputDevice
                {
                    Path = "/dev/input/event-gsr",
                    Name = "gsr-ui virtual keyboard",
                    IsVirtual = true,
                    IsKeyboard = true,
                    VendorId = 0xdec0,
                    ProductId = 0x5eba,
                },
            ],
            _ => virtualKeyboardReader);

        var received = new List<CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>();
        var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, received.Add);

        Assert.True(result.Success);
        Assert.Equal(1, result.StartedDeviceCount);
        Assert.Equal(1, virtualKeyboardReader.StartCalls);

        virtualKeyboardReader.EmitReport(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event { type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_KEY, code = 30, value = 1 });

        Assert.Equal(2, received.Count);
        Assert.Equal(30, received[0].code);
    }

    [Fact]
    public async Task ActiveCapture_WhenDeviceIsAdded_StartsReaderWithoutRestartingCapture()
    {
        var devices = new List<InputDeviceHelper.InputDevice>
        {
            CreateKeyboard("/dev/input/event-initial"),
        };
        var deviceLock = new Lock();
        var readers = new Dictionary<string, FakeLinuxCaptureReader>(StringComparer.Ordinal);
        var keyboardAdded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new InputCaptureManager(
            () =>
            {
                lock (deviceLock)
                {
                    return devices.ToArray();
                }
            },
            device =>
            {
                var reader = new FakeLinuxCaptureReader();
                readers.Add(device.Path, reader);
                if (device.Path is "/dev/input/event-reconnected")
                {
                    _ = keyboardAdded.TrySetResult();
                }

                return reader;
            },
            rescanInterval: TimeSpan.FromMilliseconds(10));

        using (manager)
        {
            var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, _ => { });
            Assert.True(result.Success);

            lock (deviceLock)
            {
                devices.Add(CreateKeyboard("/dev/input/event-reconnected"));
            }
            await keyboardAdded.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(1, readers["/dev/input/event-initial"].StartCalls);
            Assert.Equal(1, readers["/dev/input/event-reconnected"].StartCalls);
        }
    }

    [Fact]
    public async Task ActiveCapture_UsesDedicatedEnumeratorForPeriodicRescans()
    {
        var device = CreateKeyboard("/dev/input/event-keyboard");
        var initialEnumerationCalls = 0;
        var rescanCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new InputCaptureManager(
            () =>
            {
                initialEnumerationCalls++;
                return [device];
            },
            _ => new FakeLinuxCaptureReader(),
            () =>
            {
                _ = rescanCompleted.TrySetResult();
                return [device];
            },
            rescanInterval: TimeSpan.FromMilliseconds(10));

        using (manager)
        {
            var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, _ => { });

            Assert.True(result.Success);
            await rescanCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(1, initialEnumerationCalls);
        }
    }

    [Fact]
    public async Task ActiveCapture_WhenDeviceIsRemoved_DisposesOnlyItsReader()
    {
        var first = CreateKeyboard("/dev/input/event-first");
        var second = CreateKeyboard("/dev/input/event-second");
        var devices = new List<InputDeviceHelper.InputDevice> { first, second };
        var deviceLock = new Lock();
        var readers = new Dictionary<string, FakeLinuxCaptureReader>(StringComparer.Ordinal);
        var firstDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new InputCaptureManager(
            () =>
            {
                lock (deviceLock)
                {
                    return devices.ToArray();
                }
            },
            device =>
            {
                var reader = new FakeLinuxCaptureReader
                {
                    Disposed = () =>
                    {
                        if (device.Path is "/dev/input/event-first")
                        {
                            _ = firstDisposed.TrySetResult();
                        }
                    },
                };
                readers.Add(device.Path, reader);
                return reader;
            },
            rescanInterval: TimeSpan.FromMilliseconds(10));

        using (manager)
        {
            var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, _ => { });
            Assert.True(result.Success);

            lock (deviceLock)
            {
                _ = devices.Remove(first);
            }
            await firstDisposed.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(1, readers[first.Path].DisposeCalls);
            Assert.Equal(0, readers[second.Path].DisposeCalls);
        }
    }

    [Fact]
    public async Task ActiveCapture_WhenReaderStopsListening_ReopensItsCurrentDevicePath()
    {
        var device = CreateKeyboard("/dev/input/event-keyboard");
        var readers = new List<FakeLinuxCaptureReader>();
        var reopened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new InputCaptureManager(
            () => [device],
            _device =>
            {
                var reader = new FakeLinuxCaptureReader();
                readers.Add(reader);
                if (readers.Count is 2)
                {
                    _ = reopened.TrySetResult();
                }

                return reader;
            },
            rescanInterval: TimeSpan.FromMilliseconds(10));

        using (manager)
        {
            var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, _ => { });
            Assert.True(result.Success);

            readers[0].IsListening = false;
            await reopened.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(1, readers[0].DisposeCalls);
            Assert.Equal(1, readers[1].StartCalls);
        }
    }

    [Fact]
    public async Task ActiveCapture_WhenReconnectedDeviceFailsToOpen_RetriesOnTheNextRescan()
    {
        var initial = CreateKeyboard("/dev/input/event-initial");
        var reconnected = CreateKeyboard("/dev/input/event-reconnected");
        var devices = new List<InputDeviceHelper.InputDevice> { initial };
        var deviceLock = new Lock();
        var reconnectAttempts = 0;
        var reopened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = new InputCaptureManager(
            () =>
            {
                lock (deviceLock)
                {
                    return devices.ToArray();
                }
            },
            device =>
            {
                if (device.Path is not "/dev/input/event-reconnected")
                {
                    return new FakeLinuxCaptureReader();
                }

                reconnectAttempts++;
                if (reconnectAttempts is 1)
                {
                    return new FakeLinuxCaptureReader { ThrowOnStart = true };
                }

                _ = reopened.TrySetResult();
                return new FakeLinuxCaptureReader();
            },
            rescanInterval: TimeSpan.FromMilliseconds(10));

        using (manager)
        {
            var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, _ => { });
            Assert.True(result.Success);

            lock (deviceLock)
            {
                devices.Add(reconnected);
            }
            await reopened.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(2, reconnectAttempts);
        }
    }

    [Fact]
    public async Task StopCapture_PreventsRescanFromStartingNewReaders()
    {
        var devices = new List<InputDeviceHelper.InputDevice>
        {
            CreateKeyboard("/dev/input/event-initial"),
        };
        var deviceLock = new Lock();
        var readerFactoryCalls = 0;
        var manager = new InputCaptureManager(
            () =>
            {
                lock (deviceLock)
                {
                    return devices.ToArray();
                }
            },
            _ =>
            {
                readerFactoryCalls++;
                return new FakeLinuxCaptureReader();
            },
            rescanInterval: TimeSpan.FromMilliseconds(10));

        using (manager)
        {
            var result = manager.StartCapture(captureMouse: false, captureKeyboard: true, _ => { });
            Assert.True(result.Success);

            manager.StopCapture();
            lock (deviceLock)
            {
                devices.Add(CreateKeyboard("/dev/input/event-reconnected"));
            }

            await Task.Delay(100);

            Assert.Equal(1, readerFactoryCalls);
        }
    }

    private static InputDeviceHelper.InputDevice CreateKeyboard(string path) => new()
    {
        Path = path,
        Name = "Test Keyboard",
        IsKeyboard = true,
    };

    private sealed class FakeLinuxCaptureReader : InputCaptureManager.ILinuxCaptureReader
    {
        private event Action<InputCaptureManager.ILinuxCaptureReader, CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event>? EventReceivedInternal;

        public int StartCalls { get; private set; }

        public bool ThrowOnStart { get; init; }

        public bool IsListening { get; set; }

        public int DisposeCalls { get; private set; }

        public Action? Disposed { get; init; }

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

            IsListening = true;
        }

        public void Dispose()
        {
            DisposeCalls++;
            IsListening = false;
            Disposed?.Invoke();
        }

        public void Emit(CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event inputEvent)
        {
            EventReceivedInternal?.Invoke(this, inputEvent);
        }

        public void EmitReport(params CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event[] events)
        {
            foreach (var inputEvent in events)
            {
                Emit(inputEvent);
            }

            Emit(new CrossMacro.Platform.Linux.Native.UInput.UInputNative.input_event
            {
                type = CrossMacro.Platform.Linux.Native.UInput.UInputNative.EV_SYN,
                code = CrossMacro.Platform.Linux.Native.UInput.UInputNative.SYN_REPORT,
            });
        }
    }
}
