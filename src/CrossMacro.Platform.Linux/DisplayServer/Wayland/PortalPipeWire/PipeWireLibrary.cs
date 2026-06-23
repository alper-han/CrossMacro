using CrossMacro.Platform.Linux.Native;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed class PipeWireLibrary : IDisposable
{
    private static readonly string[] LibraryNames = ["libpipewire-0.3.so.0", "libpipewire-0.3.so"];

    public delegate void StreamStateChanged(IntPtr data, int oldState, int state, IntPtr error);
    public delegate void StreamParamChanged(IntPtr data, uint id, IntPtr parameter);
    public delegate void StreamBufferChanged(IntPtr data, IntPtr buffer);
    public delegate void StreamProcess(IntPtr data);

    private delegate void PwInit(IntPtr argc, IntPtr argv);
    private delegate IntPtr PwThreadLoopNew(string name, IntPtr props);
    private delegate void PwThreadLoopDestroy(IntPtr loop);
    private delegate IntPtr PwThreadLoopGetLoop(IntPtr loop);
    private delegate int PwThreadLoopStart(IntPtr loop);
    private delegate void PwThreadLoopStop(IntPtr loop);
    private delegate void PwThreadLoopLock(IntPtr loop);
    private delegate void PwThreadLoopUnlock(IntPtr loop);
    private delegate int PwThreadLoopTimedWait(IntPtr loop, int waitMaxSec);
    private delegate void PwThreadLoopSignal(IntPtr loop, [MarshalAs(UnmanagedType.I1)] bool waitForAccept);
    private delegate IntPtr PwContextNew(IntPtr mainLoop, IntPtr props, UIntPtr userDataSize);
    private delegate void PwContextDestroy(IntPtr context);
    private delegate IntPtr PwContextConnectFd(IntPtr context, int fd, IntPtr props, UIntPtr userDataSize);
    private delegate int PwCoreDisconnect(IntPtr core);
    private delegate IntPtr PwPropertiesNew(string key, string value, IntPtr sentinel);
    private delegate int PwPropertiesSet(IntPtr props, string key, string value);
    private delegate IntPtr PwStreamNew(IntPtr core, string name, IntPtr props);
    private delegate void PwStreamDestroy(IntPtr stream);
    private delegate void PwStreamAddListener(IntPtr stream, IntPtr listener, IntPtr events, IntPtr data);
    private delegate int PwStreamConnect(IntPtr stream, PipeWireDirection direction, uint targetId, PipeWireStreamFlags flags, IntPtr parameters, uint parameterCount);
    private delegate int PwStreamUpdateParams(IntPtr stream, IntPtr parameters, uint parameterCount);
    private delegate IntPtr PwStreamDequeueBuffer(IntPtr stream);
    private delegate int PwStreamQueueBuffer(IntPtr stream, IntPtr buffer);

    private readonly IntPtr _handle;
    private readonly PwThreadLoopNew _threadLoopNew;
    private readonly PwThreadLoopDestroy _threadLoopDestroy;
    private readonly PwThreadLoopGetLoop _threadLoopGetLoop;
    private readonly PwThreadLoopStart _threadLoopStart;
    private readonly PwThreadLoopStop _threadLoopStop;
    private readonly PwThreadLoopLock _threadLoopLock;
    private readonly PwThreadLoopUnlock _threadLoopUnlock;
    private readonly PwThreadLoopTimedWait _threadLoopTimedWait;
    private readonly PwThreadLoopSignal _threadLoopSignal;
    private readonly PwContextNew _contextNew;
    private readonly PwContextDestroy _contextDestroy;
    private readonly PwContextConnectFd _contextConnectFd;
    private readonly PwCoreDisconnect _coreDisconnect;
    private readonly PwPropertiesNew _propertiesNew;
    private readonly PwPropertiesSet _propertiesSet;
    private readonly PwStreamNew _streamNew;
    private readonly PwStreamDestroy _streamDestroy;
    private readonly PwStreamAddListener _streamAddListener;
    private readonly PwStreamConnect _streamConnect;
    private readonly PwStreamUpdateParams _streamUpdateParams;
    private readonly PwStreamDequeueBuffer _streamDequeueBuffer;
    private readonly PwStreamQueueBuffer _streamQueueBuffer;
    private bool _disposed;

    private PipeWireLibrary(IntPtr handle)
    {
        _handle = handle;
        Resolve<PwInit>("pw_init")(IntPtr.Zero, IntPtr.Zero);
        _threadLoopNew = Resolve<PwThreadLoopNew>("pw_thread_loop_new");
        _threadLoopDestroy = Resolve<PwThreadLoopDestroy>("pw_thread_loop_destroy");
        _threadLoopGetLoop = Resolve<PwThreadLoopGetLoop>("pw_thread_loop_get_loop");
        _threadLoopStart = Resolve<PwThreadLoopStart>("pw_thread_loop_start");
        _threadLoopStop = Resolve<PwThreadLoopStop>("pw_thread_loop_stop");
        _threadLoopLock = Resolve<PwThreadLoopLock>("pw_thread_loop_lock");
        _threadLoopUnlock = Resolve<PwThreadLoopUnlock>("pw_thread_loop_unlock");
        _threadLoopTimedWait = Resolve<PwThreadLoopTimedWait>("pw_thread_loop_timed_wait");
        _threadLoopSignal = Resolve<PwThreadLoopSignal>("pw_thread_loop_signal");
        _contextNew = Resolve<PwContextNew>("pw_context_new");
        _contextDestroy = Resolve<PwContextDestroy>("pw_context_destroy");
        _contextConnectFd = Resolve<PwContextConnectFd>("pw_context_connect_fd");
        _coreDisconnect = Resolve<PwCoreDisconnect>("pw_core_disconnect");
        _propertiesNew = Resolve<PwPropertiesNew>("pw_properties_new");
        _propertiesSet = Resolve<PwPropertiesSet>("pw_properties_set");
        _streamNew = Resolve<PwStreamNew>("pw_stream_new");
        _streamDestroy = Resolve<PwStreamDestroy>("pw_stream_destroy");
        _streamAddListener = Resolve<PwStreamAddListener>("pw_stream_add_listener");
        _streamConnect = Resolve<PwStreamConnect>("pw_stream_connect");
        _streamUpdateParams = Resolve<PwStreamUpdateParams>("pw_stream_update_params");
        _streamDequeueBuffer = Resolve<PwStreamDequeueBuffer>("pw_stream_dequeue_buffer");
        _streamQueueBuffer = Resolve<PwStreamQueueBuffer>("pw_stream_queue_buffer");
    }

    public static bool CanLoad()
    {
        if (!NativeLibraryLoader.TryLoad(LibraryNames, out var handle))
        {
            return false;
        }

        NativeLibrary.Free(handle);
        return true;
    }
    public static PipeWireLibrary Load() => new(NativeLibraryLoader.Load(LibraryNames, "libpipewire-0.3"));

    public IntPtr ThreadLoopNew(string name, IntPtr props) => _threadLoopNew(name, props);
    public void ThreadLoopDestroy(IntPtr loop) => _threadLoopDestroy(loop);
    public IntPtr ThreadLoopGetLoop(IntPtr loop) => _threadLoopGetLoop(loop);
    public int ThreadLoopStart(IntPtr loop) => _threadLoopStart(loop);
    public void ThreadLoopStop(IntPtr loop) => _threadLoopStop(loop);
    public void ThreadLoopLock(IntPtr loop) => _threadLoopLock(loop);
    public void ThreadLoopUnlock(IntPtr loop) => _threadLoopUnlock(loop);
    public int ThreadLoopTimedWait(IntPtr loop, int waitMaxSec) => _threadLoopTimedWait(loop, waitMaxSec);
    public void ThreadLoopSignal(IntPtr loop, bool waitForAccept) => _threadLoopSignal(loop, waitForAccept);
    public IntPtr ContextNew(IntPtr mainLoop, IntPtr props, UIntPtr userDataSize) => _contextNew(mainLoop, props, userDataSize);
    public void ContextDestroy(IntPtr context) => _contextDestroy(context);
    public IntPtr ContextConnectFd(IntPtr context, int fd, IntPtr props, UIntPtr userDataSize) => _contextConnectFd(context, fd, props, userDataSize);
    public int CoreDisconnect(IntPtr core) => _coreDisconnect(core);
    public IntPtr PropertiesNew(string key, string value) => _propertiesNew(key, value, IntPtr.Zero);
    public int PropertiesSet(IntPtr props, string key, string value) => _propertiesSet(props, key, value);
    public IntPtr StreamNew(IntPtr core, string name, IntPtr props) => _streamNew(core, name, props);
    public void StreamDestroy(IntPtr stream) => _streamDestroy(stream);
    public void StreamAddListener(IntPtr stream, IntPtr listener, IntPtr events, IntPtr data) => _streamAddListener(stream, listener, events, data);
    public int StreamConnect(IntPtr stream, PipeWireDirection direction, uint targetId, PipeWireStreamFlags flags, IntPtr parameters, uint parameterCount) => _streamConnect(stream, direction, targetId, flags, parameters, parameterCount);
    public int StreamUpdateParams(IntPtr stream, IntPtr parameters, uint parameterCount) => _streamUpdateParams(stream, parameters, parameterCount);
    public IntPtr StreamDequeueBuffer(IntPtr stream) => _streamDequeueBuffer(stream);
    public int StreamQueueBuffer(IntPtr stream, IntPtr buffer) => _streamQueueBuffer(stream, buffer);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NativeLibrary.Free(_handle);
    }

    private T Resolve<T>(string symbol) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_handle, symbol));
}
