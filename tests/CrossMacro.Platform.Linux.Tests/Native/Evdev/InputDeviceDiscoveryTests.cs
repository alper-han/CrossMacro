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

    [Fact]
    public void Discover_ReusesOneParsedProcSnapshotPerScanButRefreshesOnNextScan()
    {
        var source = new FakeSource(["event1", "event2"])
        {
            ProcContent = "N: Name=\"Device\"\nH: Handlers=kbd event1\n",
        };
        var discovery = new InputDeviceDiscovery(source);

        _ = discovery.GetAvailableDevices(logSummary: false, logInaccessibleWarning: false);
        source.ProcContent = "N: Name=\"Device\"\nH: Handlers=mouse0 event1\n";
        _ = discovery.GetAvailableDevices(logSummary: false, logInaccessibleWarning: false);

        Assert.Same(source.Snapshots[0], source.Snapshots[1]);
        Assert.Same(source.Snapshots[2], source.Snapshots[3]);
        Assert.NotSame(source.Snapshots[0], source.Snapshots[2]);
        Assert.True(source.Snapshots[0].HasHandler("event1", "Device", InputDeviceHandlers.Keyboard));
        Assert.False(source.Snapshots[2].HasHandler("event1", "Device", InputDeviceHandlers.Keyboard));
        Assert.Equal(2, source.ProcReads);
    }

    [Fact]
    public async Task AccessProbe_WithOnlySyncInjection_UsesThatDependencyOnAsyncPath()
    {
        var calls = 0;
        var probe = new LinuxInputDeviceAccessProbe(() =>
        {
            calls++;
            return false;
        });

        Assert.False(await probe.HasUsableReadableInputDevicesAsync(CancellationToken.None));
        Assert.Equal(1, calls);
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
            return (CanOpen?.Invoke(path) ?? true, EvdevErrorCodes.AccessDenied);
        }
    }
}
