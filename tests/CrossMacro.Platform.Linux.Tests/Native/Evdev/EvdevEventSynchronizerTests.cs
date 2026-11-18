namespace CrossMacro.Platform.Linux.Tests.Native.Evdev;

public sealed class EvdevEventSynchronizerTests
{
    private const ushort KeyA = 30;
    private const ushort KeyB = 48;
    private const ushort KeyC = 46;

    private readonly List<UInputNative.input_event> _emitted = [];
    private readonly HashSet<ushort> _deviceKeysDown = [];
    private bool _snapshotAvailable = true;

    [Fact]
    public void Process_AfterSynDropped_DiscardsEventsUntilSynReportThenEmitsReport()
    {
        var synchronizer = CreateSynchronizer();

        synchronizer.Process(Syn(UInputNative.SYN_DROPPED));
        synchronizer.Process(Key(KeyA, 1));
        synchronizer.Process(Key(KeyA, 0));
        Assert.Empty(_emitted);

        synchronizer.Process(Syn(UInputNative.SYN_REPORT));

        var report = Assert.Single(_emitted);
        Assert.Equal(UInputNative.EV_SYN, report.type);
        Assert.Equal(UInputNative.SYN_REPORT, report.code);
    }

    [Fact]
    public void Resync_KeyHeldAcrossDrop_IsNotReportedAgainAsPress()
    {
        var synchronizer = CreateSynchronizer();
        synchronizer.Process(Key(KeyA, 1));
        _deviceKeysDown.Add(KeyA);
        _emitted.Clear();

        Drop(synchronizer);

        Assert.DoesNotContain(_emitted, e => e.type is UInputNative.EV_KEY);
    }

    [Fact]
    public void Resync_KeyReleasedDuringDrop_EmitsRelease()
    {
        var synchronizer = CreateSynchronizer();
        synchronizer.Process(Key(KeyA, 1));
        _emitted.Clear();
        _deviceKeysDown.Clear();

        Drop(synchronizer);

        var release = Assert.Single(_emitted, e => e.type is UInputNative.EV_KEY);
        Assert.Equal(KeyA, release.code);
        Assert.Equal(0, release.value);
    }

    [Fact]
    public void Resync_KeyPressedDuringDrop_EmitsPress()
    {
        var synchronizer = CreateSynchronizer();
        _deviceKeysDown.Add(KeyB);

        Drop(synchronizer);

        var press = Assert.Single(_emitted, e => e.type is UInputNative.EV_KEY);
        Assert.Equal(KeyB, press.code);
        Assert.Equal(1, press.value);
    }

    [Fact]
    public void Resync_AfterSeed_DoesNotReportKeysHeldBeforeStartAsPresses()
    {
        _deviceKeysDown.Add(KeyC);
        var synchronizer = CreateSynchronizer();
        synchronizer.SeedKeyState();

        Drop(synchronizer);

        Assert.DoesNotContain(_emitted, e => e.type is UInputNative.EV_KEY);
    }

    [Fact]
    public void Resync_SecondDrop_DiffsAgainstKeysForwardedSinceTheFirstDrop()
    {
        var synchronizer = CreateSynchronizer();
        _deviceKeysDown.Add(KeyA);
        Drop(synchronizer);
        Assert.Single(_emitted, e => e.type is UInputNative.EV_KEY && e.code is KeyA && e.value is 1);

        synchronizer.Process(Key(KeyA, 0));
        _deviceKeysDown.Clear();
        _emitted.Clear();

        Drop(synchronizer);

        Assert.DoesNotContain(_emitted, e => e.type is UInputNative.EV_KEY);
    }

    [Fact]
    public void Resync_WhenSnapshotUnavailable_StillForwardsTheClosingReport()
    {
        var synchronizer = CreateSynchronizer();
        _deviceKeysDown.Add(KeyA);
        _snapshotAvailable = false;

        Drop(synchronizer);

        var report = Assert.Single(_emitted);
        Assert.Equal(UInputNative.SYN_REPORT, report.code);
    }

    [Fact]
    public void Process_OutOfRangeKeyCode_IsForwardedWithoutTracking()
    {
        var synchronizer = CreateSynchronizer();

        synchronizer.Process(Key(0x2ff + 1, 1));

        _ = Assert.Single(_emitted);
    }

    private EvdevEventSynchronizer CreateSynchronizer() =>
        new("test-device", ReadSnapshot, _emitted.Add);

    private bool ReadSnapshot(byte[] destination)
    {
        if (!_snapshotAvailable)
        {
            return false;
        }

        Array.Clear(destination);
        foreach (var key in _deviceKeysDown)
        {
            destination[key / 8] |= (byte)(1 << (key % 8));
        }

        return true;
    }

    private static void Drop(EvdevEventSynchronizer synchronizer)
    {
        synchronizer.Process(Syn(UInputNative.SYN_DROPPED));
        synchronizer.Process(Syn(UInputNative.SYN_REPORT));
    }

    private static UInputNative.input_event Syn(ushort code) =>
        new() { type = UInputNative.EV_SYN, code = code };

    private static UInputNative.input_event Key(int code, int value) =>
        new() { type = UInputNative.EV_KEY, code = (ushort)code, value = value };
}
