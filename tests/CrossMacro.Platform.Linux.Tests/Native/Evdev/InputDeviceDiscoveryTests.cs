using InputDevice = CrossMacro.Platform.Linux.Native.Evdev.InputDeviceHelper.InputDevice;

namespace CrossMacro.Platform.Linux.Tests.Native.Evdev;

public sealed class InputDeviceDiscoveryTests
{
    [Fact]
    public async Task DiscoverAsync_WhenAlreadyCanceled_DoesNotTouchTheSource()
    {
        var source = new FakeSource(["event1"]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new InputDeviceDiscovery(source).GetAvailableDevicesAsync(logInaccessibleWarning: false, cancellation.Token));

        Assert.Equal(0, source.DirectoryChecks);
        Assert.Equal(0, source.ProcReads);
        Assert.Empty(source.DeviceReads);
    }

    [Fact]
    public async Task DiscoverAsync_WhenProcReadIsCanceled_PropagatesInsteadOfFallingBackToDeviceIo()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new FakeSource(["event1"])
        {
            ReadProcAsync = async token =>
            {
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
                return null;
            },
        };

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new InputDeviceDiscovery(source).GetAvailableDevicesAsync(logInaccessibleWarning: false, cancellation.Token));

        Assert.Empty(source.DeviceReads);
    }

    [Fact]
    public async Task DiscoverAsync_WhenCanceledDuringOneDevice_DoesNotInspectTheNextDeviceOrReturnPartialResults()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new FakeSource(["event1", "event2"])
        {
            DeviceReader = (path, _) =>
            {
                cancellation.Cancel();
                return new InputDevice { Path = path, IsKeyboard = true };
            },
        };

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new InputDeviceDiscovery(source).GetAvailableDevicesAsync(logInaccessibleWarning: false, cancellation.Token));

        Assert.Equal(["event1"], source.DeviceReads);
        Assert.Equal(0, source.OpenChecks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Discover_WhenProcReadFails_PreservesNativeFallbackAndEnumerationOrder(bool useAsync)
    {
        var source = new FakeSource(["event3", "event1", "event2"])
        {
            ProcFailure = new IOException("proc unavailable"),
        };
        var discovery = new InputDeviceDiscovery(source);

        var devices = useAsync
            ? await discovery.GetAvailableDevicesAsync(logInaccessibleWarning: false, CancellationToken.None)
            : discovery.GetAvailableDevices(logSummary: false, logInaccessibleWarning: false);

        Assert.Equal(["event3", "event1", "event2"], devices.Select(device => device.Path), StringComparer.Ordinal);
        Assert.Equal(["event3", "event1", "event2"], source.DeviceReads);
        Assert.All(source.Snapshots, snapshot => Assert.False(snapshot.HasHandler("event1", "Device", InputDeviceHandlers.Keyboard)));
    }

    [Fact]
    public void Discover_ToleratesVanishedDeniedBusyAndExcludedDevicesWhileKeepingUsableDevices()
    {
        var source = new FakeSource(["event1", "gone", "denied", "busy", "broken", "excluded", "unreadable", "event2"])
        {
            DeviceReader = (path, _) => path switch
            {
                "gone" => throw new InputDeviceHelper.DeviceOpenException(path, EvdevErrorCodes.NotFound),
                "denied" => throw new InputDeviceHelper.DeviceOpenException(path, EvdevErrorCodes.AccessDenied),
                "busy" => throw new InputDeviceHelper.DeviceOpenException(path, EvdevErrorCodes.Busy),
                "broken" => throw new IOException("device read failed"),
                "excluded" => new InputDevice { Path = path },
                _ => new InputDevice { Path = path, IsKeyboard = true },
            },
            CanOpen = path => !string.Equals(path, "unreadable", StringComparison.Ordinal),
        };

        var devices = new InputDeviceDiscovery(source).GetAvailableDevices(logSummary: false, logInaccessibleWarning: false);

        Assert.Equal(["event1", "event2"], devices.Select(device => device.Path), StringComparer.Ordinal);
        Assert.Equal(8, source.DeviceReads.Count);
        Assert.Equal(3, source.OpenChecks);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task AccessProbe_DeniedPresentKeyboardBlocksReadinessWithoutHidingReadableMouse(bool denyInitialRead, bool hasKernelHandler)
    {
        var granted = false;
        var source = new FakeSource(["/dev/input/event0", "/dev/input/event1"])
        {
            ProcContent = "N: Name=\"Mouse\"\nH: Handlers=mouse0 event0\n\nN: Name=\"Keyboard\"\nH: Handlers=sysrq " + (hasKernelHandler ? "kbd " : "") + "event1\n",
            DeviceReader = (path, _) =>
            {
                if (path.EndsWith("event1", StringComparison.Ordinal) && denyInitialRead && !granted)
                {
                    throw new InputDeviceHelper.DeviceOpenException(path, EvdevErrorCodes.AccessDenied);
                }
                return new InputDevice { Path = path, IsMouse = path.EndsWith("event0", StringComparison.Ordinal), IsKeyboard = path.EndsWith("event1", StringComparison.Ordinal) };
            },
            CanOpen = path => granted || path.EndsWith("event0", StringComparison.Ordinal),
        };
        var discovery = new InputDeviceDiscovery(source);
        var probe = new LinuxInputDeviceAccessProbe(discovery);

        Assert.Equal("/dev/input/event0", Assert.Single(discovery.GetAvailableDevices(logSummary: false, logInaccessibleWarning: false)).Path);
        Assert.False(probe.HasUsableReadableInputDevices());
        Assert.False(await probe.HasUsableReadableInputDevicesAsync(CancellationToken.None));

        granted = true;
        Assert.True(probe.HasUsableReadableInputDevices());
        Assert.True(await probe.HasUsableReadableInputDevicesAsync(CancellationToken.None));
        Assert.Equal(["/dev/input/event0", "/dev/input/event1"], discovery.GetAvailableDevices(logSummary: false, logInaccessibleWarning: false).Select(device => device.Path), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AccessProbe_OnlyOnePresentInputClassRemainsReady(bool isMouse)
    {
        var source = new FakeSource(["/dev/input/event0"])
        {
            DeviceReader = (path, _) => new InputDevice { Path = path, IsMouse = isMouse, IsKeyboard = !isMouse },
        };
        var probe = new LinuxInputDeviceAccessProbe(new InputDeviceDiscovery(source));

        Assert.True(probe.HasUsableReadableInputDevices());
        Assert.True(await probe.HasUsableReadableInputDevicesAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("Power Button", "kbd", "", true)]
    [InlineData("Lid Switch", "kbd", "", true)]
    [InlineData("CrossMacro Virtual Input Device", "mouse0 kbd", "I: Bus=0006 Vendor=1234 Product=5678 Version=0001\n", true)]
    [InlineData("CrossMacro Virtual Input Device", "kbd", "I: Bus=0003 Vendor=abcd Product=5678 Version=0001\n", false)]
    [InlineData("External Virtual Keyboard", "kbd", "", false)]
    [InlineData("Touchpad", "mouse2", "", false)]
    public async Task AccessProbe_DeniedMetadataUsesExistingExclusionsAndExactVirtualIdentity(string name, string handlers, string identity, bool expected)
    {
        var source = new FakeSource(["/dev/input/event0", "/dev/input/event1"])
        {
            ProcContent = identity + "N: Name=\"" + name + "\"\nH: Handlers=" + handlers + " event1\n",
            DeviceReader = (path, _) => path.EndsWith("event1", StringComparison.Ordinal)
                ? throw new InputDeviceHelper.DeviceOpenException(path, EvdevErrorCodes.AccessDenied)
                : new InputDevice { Path = path, IsMouse = true },
        };
        var probe = new LinuxInputDeviceAccessProbe(new InputDeviceDiscovery(source));

        Assert.Equal(expected, probe.HasUsableReadableInputDevices());
        Assert.Equal(expected, await probe.HasUsableReadableInputDevicesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AccessProbe_DeniedDeviceWithoutMetadataCannotEstablishReadiness()
    {
        var source = new FakeSource(["/dev/input/event0", "/dev/input/event1"])
        {
            ProcFailure = new IOException("proc unavailable"),
            DeviceReader = (path, _) => path.EndsWith("event1", StringComparison.Ordinal)
                ? throw new InputDeviceHelper.DeviceOpenException(path, EvdevErrorCodes.AccessDenied)
                : new InputDevice { Path = path, IsMouse = true },
        };
        var probe = new LinuxInputDeviceAccessProbe(new InputDeviceDiscovery(source));

        Assert.False(probe.HasUsableReadableInputDevices());
        Assert.False(await probe.HasUsableReadableInputDevicesAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccessProbe_VanishedKeyboardDoesNotRequireAbsentInputClass(bool disappearsBeforeMetadata)
    {
        var source = new FakeSource(["/dev/input/event0", "/dev/input/event1"])
        {
            ProcContent = "N: Name=\"Keyboard\"\nH: Handlers=kbd event1\n",
            DeviceReader = (path, _) => path.EndsWith("event1", StringComparison.Ordinal) && disappearsBeforeMetadata
                ? throw new InputDeviceHelper.DeviceOpenException(path, EvdevErrorCodes.NotFound)
                : new InputDevice { Path = path, IsKeyboard = path.EndsWith("event1", StringComparison.Ordinal), IsMouse = path.EndsWith("event0", StringComparison.Ordinal) },
            CanOpen = path => path.EndsWith("event0", StringComparison.Ordinal),
            OpenError = EvdevErrorCodes.NotFound,
        };
        var probe = new LinuxInputDeviceAccessProbe(new InputDeviceDiscovery(source));

        Assert.True(probe.HasUsableReadableInputDevices());
        Assert.True(await probe.HasUsableReadableInputDevicesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AccessProbe_CancellationDuringFinalAccessCheckDoesNotReturnReadiness()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new FakeSource(["/dev/input/event0"])
        {
            CanOpen = _ => { cancellation.Cancel(); return true; },
        };
        var probe = new LinuxInputDeviceAccessProbe(new InputDeviceDiscovery(source));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await probe.HasUsableReadableInputDevicesAsync(cancellation.Token));
    }

    private sealed class FakeSource(string[] files) : IInputDeviceDiscoverySource
    {
        public int DirectoryChecks { get; private set; }
        public int ProcReads { get; private set; }
        public int OpenChecks { get; private set; }
        public string? ProcContent { get; set; }
        public Exception? ProcFailure { get; init; }
        public Func<CancellationToken, Task<string?>>? ReadProcAsync { get; init; }
        public Func<string, ProcInputDeviceSnapshot, InputDevice>? DeviceReader { get; init; }
        public Func<string, bool>? CanOpen { get; init; }
        public int OpenError { get; init; } = EvdevErrorCodes.AccessDenied;
        public List<string> DeviceReads { get; } = [];
        public List<ProcInputDeviceSnapshot> Snapshots { get; } = [];

        public bool InputDirectoryExists()
        {
            DirectoryChecks++;
            return true;
        }

        public string[] GetDeviceFiles() => files;

        public string? ReadProcDevices()
        {
            ProcReads++;
            if (ProcFailure is not null)
            {
                throw ProcFailure;
            }
            return ProcContent;
        }

        public Task<string?> ReadProcDevicesAsync(CancellationToken cancellationToken)
            => ReadProcAsync is not null ? ReadProcAsync(cancellationToken) : Task.FromResult(ReadProcDevices());

        public InputDevice ReadDevice(string path, ProcInputDeviceSnapshot procSnapshot)
        {
            DeviceReads.Add(path);
            Snapshots.Add(procSnapshot);
            return DeviceReader?.Invoke(path, procSnapshot) ?? new InputDevice { Path = path, IsKeyboard = true };
        }

        public (bool CanOpen, int Errno) CanOpenForReading(string path)
        {
            OpenChecks++;
            return (CanOpen?.Invoke(path) ?? true, OpenError);
        }
    }
}
