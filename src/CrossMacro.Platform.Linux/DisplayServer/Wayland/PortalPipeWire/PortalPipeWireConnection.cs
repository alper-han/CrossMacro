namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed class PortalPipeWireConnection : IDisposable
{
    private const int CoreEventVersion = 1;
    private const uint CoreId = 0;
    private static readonly TimeSpan SynchronizationBudget = TimeSpan.FromSeconds(1);

    private readonly PipeWireLibrary.CoreDone _coreDone;
    private readonly PipeWireLibrary.CoreError _coreError;
    private readonly GCHandle _selfHandle;
    private readonly IntPtr _context;
    private readonly IntPtr _coreListener;
    private readonly IntPtr _coreEvents;
    private readonly Lock _disposeGate = new();
    private int _lastDoneSequence = -1;
    private bool _disposed;
    private int _faulted;

    internal PortalPipeWireConnection(SafeFileHandle pipeWireRemote)
    {
        ArgumentNullException.ThrowIfNull(pipeWireRemote);
        if (pipeWireRemote.IsClosed)
        {
            throw new ArgumentException("PipeWire remote handle is closed.", nameof(pipeWireRemote));
        }

        Library = PipeWireLibrary.Load();
        _coreDone = OnCoreDone;
        _coreError = OnCoreError;
        _selfHandle = GCHandle.Alloc(this);
        try
        {
            ThreadLoop = CreateThreadLoop(Library);
            Library.ThreadLoopLock(ThreadLoop);
            try
            {
                _context = Library.ContextNew(Library.ThreadLoopGetLoop(ThreadLoop), IntPtr.Zero, UIntPtr.Zero);
                if (_context == IntPtr.Zero)
                {
                    throw new InvalidOperationException("pw_context_new failed.");
                }

                Core = ConnectCore(pipeWireRemote);
                (_coreListener, _coreEvents) = AddCoreListener();
            }
            finally
            {
                Library.ThreadLoopUnlock(ThreadLoop);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public event Action<string>? Error;

    public PipeWireLibrary Library { get; }

    public IntPtr ThreadLoop { get; }

    public IntPtr Core { get; }

    public static PortalPipeWireConnectionLease Acquire(SafeFileHandle pipeWireRemote) =>
        PortalPipeWireConnectionRegistry.Acquire(pipeWireRemote);

    public void WithLock(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(_disposed, this);
        Library.ThreadLoopLock(ThreadLoop);
        try
        {
            action();
        }
        finally
        {
            Library.ThreadLoopUnlock(ThreadLoop);
        }
    }

    public void Dispose()
    {
        lock (_disposeGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (ThreadLoop != IntPtr.Zero)
            {
                Library.ThreadLoopLock(ThreadLoop);
                try
                {
                    if (Core != IntPtr.Zero)
                    {
                        SynchronizeLocked();
                        _ = Library.CoreDisconnect(Core);
                    }
                }
                finally
                {
                    Library.ThreadLoopUnlock(ThreadLoop);
                }

                Library.ThreadLoopStop(ThreadLoop);
            }

            if (_context != IntPtr.Zero)
            {
                Library.ContextDestroy(_context);
            }

            if (ThreadLoop != IntPtr.Zero)
            {
                Library.ThreadLoopDestroy(ThreadLoop);
            }

            Free(_coreListener);
            Free(_coreEvents);
            if (_selfHandle.IsAllocated)
            {
                _selfHandle.Free();
            }

            Library.Dispose();
        }
    }

    private IntPtr ConnectCore(SafeFileHandle pipeWireRemote)
    {
        var fd = PortalPipeWireLibc.dup(pipeWireRemote);
        if (fd < 0)
        {
            throw new InvalidOperationException($"dup(pipewire fd) failed errno={Marshal.GetLastPInvokeError().ToString(CultureInfo.InvariantCulture)}.");
        }

        var core = Library.ContextConnectFd(_context, fd, IntPtr.Zero, UIntPtr.Zero);
        if (core != IntPtr.Zero)
        {
            return core;
        }

        throw new InvalidOperationException("pw_context_connect_fd failed.");
    }

    private (IntPtr Listener, IntPtr Events) AddCoreListener()
    {
        var listener = IntPtr.Zero;
        var events = IntPtr.Zero;
        try
        {
            listener = Marshal.AllocHGlobal(Marshal.SizeOf<SpaHook>());
            events = Marshal.AllocHGlobal(Marshal.SizeOf<PipeWireCoreEvents>());
            Marshal.Copy(new byte[Marshal.SizeOf<SpaHook>()], 0, listener, Marshal.SizeOf<SpaHook>());
            Marshal.StructureToPtr(new PipeWireCoreEvents
            {
                Version = CoreEventVersion,
                Done = Marshal.GetFunctionPointerForDelegate(_coreDone),
                Error = Marshal.GetFunctionPointerForDelegate(_coreError),
            }, events, fDeleteOld: false);

            var result = Library.CoreAddListener(Core, listener, events, GCHandle.ToIntPtr(_selfHandle));
            if (result < 0)
            {
                throw new InvalidOperationException($"pw_core_add_listener failed rc={result.ToString(CultureInfo.InvariantCulture)}.");
            }

            return (listener, events);
        }
        catch
        {
            Free(listener);
            Free(events);
            throw;
        }
    }

    private void SynchronizeLocked()
    {
        var sequence = Library.CoreSync(Core, CoreId, -1);
        if (sequence < 0)
        {
            return;
        }

        var deadline = Stopwatch.GetTimestamp() + (long)(SynchronizationBudget.TotalSeconds * Stopwatch.Frequency);
        while (Volatile.Read(ref _lastDoneSequence) != sequence && Stopwatch.GetTimestamp() < deadline)
        {
            _ = Library.ThreadLoopTimedWait(ThreadLoop, 1);
        }
    }

    private static IntPtr CreateThreadLoop(PipeWireLibrary library)
    {
        var loop = library.ThreadLoopNew("crossmacro-portal-pw", IntPtr.Zero);
        if (loop == IntPtr.Zero)
        {
            throw new InvalidOperationException("pw_thread_loop_new failed.");
        }

        var result = library.ThreadLoopStart(loop);
        if (result < 0)
        {
            library.ThreadLoopDestroy(loop);
            throw new InvalidOperationException($"pw_thread_loop_start failed rc={result.ToString(CultureInfo.InvariantCulture)}.");
        }

        return loop;
    }

    private static void OnCoreDone(IntPtr data, uint id, int sequence)
    {
        if (id != CoreId)
        {
            return;
        }

        var connection = FromHandle(data);
        Volatile.Write(ref connection._lastDoneSequence, sequence);
        connection.Library.ThreadLoopSignal(connection.ThreadLoop, waitForAccept: false);
    }

    private static void OnCoreError(IntPtr data, uint id, int sequence, int result, IntPtr message)
    {
        var connection = FromHandle(data);
        if (Interlocked.Exchange(ref connection._faulted, 1) is not 0)
        {
            return;
        }

        var text = Marshal.PtrToStringAnsi(message);
        var description = string.IsNullOrWhiteSpace(text)
            ? $"PipeWire core error rc={result.ToString(CultureInfo.InvariantCulture)} id={id.ToString(CultureInfo.InvariantCulture)} sequence={sequence.ToString(CultureInfo.InvariantCulture)}."
            : $"PipeWire core error rc={result.ToString(CultureInfo.InvariantCulture)}: {text}";
        try
        {
            connection.Error?.Invoke(description);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            Log.Debug(ex, "PipeWire core error subscriber failed.");
        }
    }

    private static PortalPipeWireConnection FromHandle(IntPtr data) =>
        (PortalPipeWireConnection)(GCHandle.FromIntPtr(data).Target ?? throw new InvalidOperationException("PipeWire core callback target was released."));

    private static void Free(IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
