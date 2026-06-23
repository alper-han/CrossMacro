using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed partial class PortalPipeWireFrameCapture
{
    private unsafe (IntPtr Listener, IntPtr Events) AddListener()
    {
        var listener = IntPtr.Zero;
        var events = IntPtr.Zero;
        try
        {
            listener = Marshal.AllocHGlobal(Marshal.SizeOf<SpaHook>());
            events = Marshal.AllocHGlobal(Marshal.SizeOf<PipeWireStreamEvents>());
            NativeMemory.Clear((void*)listener, (nuint)Marshal.SizeOf<SpaHook>());
            NativeMemory.Clear((void*)events, (nuint)Marshal.SizeOf<PipeWireStreamEvents>());
            Marshal.StructureToPtr(new PipeWireStreamEvents
            {
                Version = 2,
                StateChanged = Marshal.GetFunctionPointerForDelegate(_stateChanged),
                ParamChanged = Marshal.GetFunctionPointerForDelegate(_paramChanged),
                AddBuffer = Marshal.GetFunctionPointerForDelegate(_addBuffer),
                RemoveBuffer = Marshal.GetFunctionPointerForDelegate(_removeBuffer),
                Process = Marshal.GetFunctionPointerForDelegate(_process)
            }, events, false);
            _lib.StreamAddListener(_stream, listener, events, GCHandle.ToIntPtr(_selfHandle));
            return (listener, events);
        }
        catch
        {
            Free(listener);
            Free(events);
            throw;
        }
    }

    private static void OnStateChanged(IntPtr data, int oldState, int state, IntPtr error)
    {
        var capture = FromHandle(data);
        capture._lastState = state;
        if (state == -1)
        {
            var message = Marshal.PtrToStringAnsi(error) ?? "PipeWire stream entered error state.";
            capture._error = $"{message} nodeId={capture._nodeId} size={capture._width}x{capture._height}";
            capture._lib.ThreadLoopSignal(capture._threadLoop, false);
        }
    }

    private static void OnParamChanged(IntPtr data, uint id, IntPtr parameter)
    {
        var capture = FromHandle(data);
        capture._lastParamId = id;
        capture._lib.ThreadLoopSignal(capture._threadLoop, false);
    }

    private static void OnAddBuffer(IntPtr data, IntPtr bufferPtr)
    {
        var capture = FromHandle(data);
        var pwBuffer = Marshal.PtrToStructure<PipeWireBuffer>(bufferPtr);
        if (pwBuffer.Buffer == IntPtr.Zero)
        {
            return;
        }

        var spaBuffer = Marshal.PtrToStructure<SpaBuffer>(pwBuffer.Buffer);
        if (spaBuffer.DataCount == 0 || spaBuffer.Datas == IntPtr.Zero)
        {
            return;
        }

        var stride = checked(capture._width * PipeWireConstants.Xrgb8888BytesPerPixel);
        var size = checked(stride * capture._height);
        var allocation = PortalPipeWireBufferAllocation.Create(size);
        var handle = GCHandle.Alloc(allocation);
        var data0 = Marshal.PtrToStructure<SpaData>(spaBuffer.Datas);
        data0.Type = 2;
        data0.Flags = 0x1 | 0x2 | 0x8;
        data0.Fd = allocation.Fd;
        data0.MapOffset = 0;
        data0.MaxSize = (uint)size;
        data0.Data = allocation.Address;
        if (data0.Chunk != IntPtr.Zero)
        {
            Marshal.StructureToPtr(new SpaChunk { Offset = 0, Size = (uint)size, Stride = stride, Flags = 0 }, data0.Chunk, false);
        }

        Marshal.StructureToPtr(data0, spaBuffer.Datas, false);
        pwBuffer.UserData = GCHandle.ToIntPtr(handle);
        Marshal.StructureToPtr(pwBuffer, bufferPtr, false);
        capture._allocatedBuffers++;
    }

    private static void OnRemoveBuffer(IntPtr data, IntPtr bufferPtr)
    {
        var pwBuffer = Marshal.PtrToStructure<PipeWireBuffer>(bufferPtr);
        if (pwBuffer.UserData == IntPtr.Zero)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr(pwBuffer.UserData);
        if (handle.Target is PortalPipeWireBufferAllocation allocation)
        {
            allocation.Dispose();
        }

        handle.Free();
        pwBuffer.UserData = IntPtr.Zero;
        Marshal.StructureToPtr(pwBuffer, bufferPtr, false);
    }

    private static void OnProcess(IntPtr data)
    {
        var capture = FromHandle(data);
        capture._processCallbacks++;
        var bufferPtr = capture._lib.StreamDequeueBuffer(capture._stream);
        if (bufferPtr == IntPtr.Zero)
        {
            capture._nullBuffers++;
            return;
        }

        try
        {
            capture.TryCopyFrame(bufferPtr);
        }
        finally
        {
            capture._lib.StreamQueueBuffer(capture._stream, bufferPtr);
        }
    }

    private void TryCopyFrame(IntPtr bufferPtr)
    {
        if (_frame is not null)
        {
            return;
        }

        var pwBuffer = Marshal.PtrToStructure<PipeWireBuffer>(bufferPtr);
        var spaBuffer = Marshal.PtrToStructure<SpaBuffer>(pwBuffer.Buffer);
        _lastDataCount = spaBuffer.DataCount;
        if (spaBuffer.DataCount == 0 || spaBuffer.Datas == IntPtr.Zero)
        {
            _missingDataArrays++;
            return;
        }

        var data0 = Marshal.PtrToStructure<SpaData>(spaBuffer.Datas);
        _lastDataType = data0.Type;
        _lastDataFlags = data0.Flags;
        _lastMaxSize = data0.MaxSize;
        if (data0.Data == IntPtr.Zero || data0.Chunk == IntPtr.Zero)
        {
            _missingDataPointers++;
            return;
        }

        var chunk = Marshal.PtrToStructure<SpaChunk>(data0.Chunk);
        _lastChunkOffset = chunk.Offset;
        _lastChunkSize = chunk.Size;
        _lastChunkStride = chunk.Stride;
        var stride = chunk.Stride > 0 ? chunk.Stride : _width * PipeWireConstants.Xrgb8888BytesPerPixel;
        if (stride < checked(_width * PipeWireConstants.Xrgb8888BytesPerPixel))
        {
            FailCopy($"PipeWire frame stride {stride} is smaller than the expected row width for {_width} pixels.");
            return;
        }

        var bytes = checked(stride * _height);
        if (data0.MaxSize == 0)
        {
            FailCopy("PipeWire frame data advertised maxsize=0.");
            return;
        }

        var offset = chunk.Offset % data0.MaxSize;
        var available = data0.MaxSize - offset;
        var chunkSize = chunk.Size > 0 ? Math.Min(chunk.Size, available) : available;
        if (chunkSize < checked((uint)bytes))
        {
            FailCopy($"PipeWire frame chunk is too small for the declared frame. offset={offset} size={chunk.Size} maxsize={data0.MaxSize} required={bytes}.");
            return;
        }

        if (offset > int.MaxValue)
        {
            FailCopy($"PipeWire frame chunk offset {offset} exceeds supported memory offset range.");
            return;
        }

        var pixels = new byte[bytes];
        Marshal.Copy(data0.Data + checked((int)offset), pixels, 0, pixels.Length);
        _frame = new PortalPipeWireFrame(new(0, 0, _width, _height), stride, CrossMacro.Platform.Abstractions.ScreenPixelFormat.Xrgb8888, pixels);
        _lib.ThreadLoopSignal(_threadLoop, false);
    }

    private void FailCopy(string message)
    {
        _error = message;
        _lib.ThreadLoopSignal(_threadLoop, false);
    }

    private static PortalPipeWireFrameCapture FromHandle(IntPtr data) =>
        (PortalPipeWireFrameCapture)(GCHandle.FromIntPtr(data).Target ?? throw new InvalidOperationException("PipeWire callback target was released."));
}
