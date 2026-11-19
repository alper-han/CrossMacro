namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PipeWireCoreMethodsTests
{
    private const int MethodTableSlots = 8;
    private const int NotSupported = -95;

    [Fact]
    public void AddListener_ShouldCallMethodTableEntryWithInterfaceDataAsReceiver()
    {
        var received = new List<IntPtr>();
        int AddListener(IntPtr target, IntPtr listener, IntPtr events, IntPtr data)
        {
            received.AddRange([target, listener, events, data]);
            return 7;
        }

        using var core = new FakeCore(addListener: AddListener);

        var result = PipeWireCoreMethods.AddListener(core.Address, new IntPtr(0x10), new IntPtr(0x20), new IntPtr(0x30));

        Assert.Equal(7, result);
        Assert.Equal([core.Receiver, new IntPtr(0x10), new IntPtr(0x20), new IntPtr(0x30)], received);
    }

    [Fact]
    public void Sync_ShouldCallSyncSlotAndReturnItsResult()
    {
        (uint Id, int Sequence)? received = null;
        int Sync(IntPtr target, uint id, int sequence)
        {
            received = (id, sequence);
            return 42;
        }

        using var core = new FakeCore(sync: Sync);

        var result = PipeWireCoreMethods.Sync(core.Address, 0, -1);

        Assert.Equal(42, result);
        Assert.Equal((0u, -1), received);
    }

    [Fact]
    public void Methods_ShouldReportNotSupportedWhenTableOrEntryIsMissing()
    {
        using var withoutTable = new FakeCore(tableMissing: true);
        using var withoutEntries = new FakeCore();

        Assert.Equal(NotSupported, PipeWireCoreMethods.Sync(withoutTable.Address, 0, 1));
        Assert.Equal(NotSupported, PipeWireCoreMethods.AddListener(withoutTable.Address, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
        Assert.Equal(NotSupported, PipeWireCoreMethods.Sync(withoutEntries.Address, 0, 1));
        Assert.Equal(NotSupported, PipeWireCoreMethods.AddListener(withoutEntries.Address, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
    }

    private sealed class FakeCore : IDisposable
    {
        private readonly IntPtr _table;
        private readonly List<Delegate> _keepAlive = [];

        public FakeCore(PipeWireCoreMethods.AddListenerMethod? addListener = null, PipeWireCoreMethods.SyncMethod? sync = null, bool tableMissing = false)
        {
            Address = Marshal.AllocHGlobal(Marshal.SizeOf<SpaInterface>());
            _table = Marshal.AllocHGlobal(IntPtr.Size * MethodTableSlots);
            for (var slot = 0; slot < MethodTableSlots; slot++)
            {
                Marshal.WriteIntPtr(_table, slot * IntPtr.Size, IntPtr.Zero);
            }

            Install(1, addListener);
            Install(3, sync);
            Receiver = new IntPtr(0xC0DE);
            Marshal.StructureToPtr(
                new SpaInterface { Callbacks = new SpaCallbacks { Functions = tableMissing ? IntPtr.Zero : _table, Data = Receiver } },
                Address,
                fDeleteOld: false);
        }

        public IntPtr Address { get; }

        public IntPtr Receiver { get; }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Address);
            Marshal.FreeHGlobal(_table);
            GC.KeepAlive(_keepAlive);
        }

        private void Install(int slot, Delegate? method)
        {
            if (method is null)
            {
                return;
            }

            _keepAlive.Add(method);
            Marshal.WriteIntPtr(_table, slot * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(method));
        }
    }
}
