
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

internal sealed partial class PortalPipeWireFrameCapture
{
    private (IntPtr Listener, IntPtr Events) AddListener()
    {
        var listener = IntPtr.Zero;
        var events = IntPtr.Zero;
        try
        {
            listener = Marshal.AllocHGlobal(Marshal.SizeOf<SpaHook>());
            events = Marshal.AllocHGlobal(Marshal.SizeOf<PipeWireStreamEvents>());
            Marshal.Copy(new byte[Marshal.SizeOf<SpaHook>()], 0, listener, Marshal.SizeOf<SpaHook>());
            Marshal.Copy(new byte[Marshal.SizeOf<PipeWireStreamEvents>()], 0, events, Marshal.SizeOf<PipeWireStreamEvents>());
            Marshal.StructureToPtr(new PipeWireStreamEvents
            {
                Version = 2,
                StateChanged = Marshal.GetFunctionPointerForDelegate(_stateChanged),
                ParamChanged = Marshal.GetFunctionPointerForDelegate(_paramChanged),
                AddBuffer = Marshal.GetFunctionPointerForDelegate(_addBuffer),
                RemoveBuffer = Marshal.GetFunctionPointerForDelegate(_removeBuffer),
                Process = Marshal.GetFunctionPointerForDelegate(_process),
            }, events, fDeleteOld: false);
            _lib.StreamAddListener(_stream, listener, events, GCHandle.ToIntPtr(_selfHandle));
            return (listener, events);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Free(listener);
            Free(events);
            throw;
        }
    }

    private static void OnStateChanged(IntPtr data, int oldState, int state, IntPtr error)
    {
        var capture = FromHandle(data);
        if (state == -1)
        {
            var message = Marshal.PtrToStringAnsi(error) ?? "PipeWire stream entered error state.";
            capture._error = $"{message} nodeId={capture._nodeId.ToString(CultureInfo.InvariantCulture)} size={capture._width.ToString(CultureInfo.InvariantCulture)}x{capture._height.ToString(CultureInfo.InvariantCulture)}";
            capture._frameCache.Clear();
            capture.CompletePending(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, capture._error));
            capture._lib.ThreadLoopSignal(capture._threadLoop, waitForAccept: false);
        }
    }

    private static void OnParamChanged(IntPtr data, uint id, IntPtr parameter)
    {
        var capture = FromHandle(data);
        if (id == PipeWireConstants.SpaParamFormat)
        {
            capture.HandleNegotiatedFormat(parameter);
        }

        capture._lib.ThreadLoopSignal(capture._threadLoop, waitForAccept: false);
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

        var data0 = Marshal.PtrToStructure<SpaData>(spaBuffer.Datas);
        if (data0.Chunk == IntPtr.Zero)
        {
            capture.FailCopy("PipeWire supplied a buffer without a frame chunk descriptor.");
        }
    }

    private static void OnRemoveBuffer(IntPtr data, IntPtr bufferPtr)
    {
        var pwBuffer = Marshal.PtrToStructure<PipeWireBuffer>(bufferPtr);
        ReleaseBufferUserData(ref pwBuffer);
        Marshal.StructureToPtr(pwBuffer, bufferPtr, fDeleteOld: false);
    }

    private static void ReleaseBufferUserData(ref PipeWireBuffer pwBuffer)
    {
        if (pwBuffer.UserData == IntPtr.Zero)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr(pwBuffer.UserData);
        try
        {
            if (handle.Target is PortalPipeWireBufferAllocation allocation)
            {
                allocation.Dispose();
            }
        }
        finally
        {
            handle.Free();
            pwBuffer.UserData = IntPtr.Zero;
        }
    }

    private static void OnProcess(IntPtr data)
    {
        var capture = FromHandle(data);
        var generation = capture._frameSequence.BeginProcess();

        var bufferPtr = capture._lib.StreamDequeueBuffer(capture._stream);
        if (bufferPtr == IntPtr.Zero)
        {
            return;
        }

        try
        {
            capture.TryCopyFrame(bufferPtr, generation);
        }
        catch (Exception ex) when (ex is not StackOverflowException)
        {
            capture.FailCopy($"PipeWire frame processing failed: {ex.Message}");
        }
        finally
        {
            var queueResult = capture._lib.StreamQueueBuffer(capture._stream, bufferPtr);
            if (queueResult < 0)
            {
                capture.FailCopy($"pw_stream_queue_buffer failed rc={queueResult.ToString(CultureInfo.InvariantCulture)}.");
            }
        }
    }

    private void TryCopyFrame(IntPtr bufferPtr, long generation)
    {
        if (Volatile.Read(ref _disposed))
        {
            return;
        }

        PendingCapture? pending;
        lock (_pendingGate)
        {
            pending = _pending;
        }

        var completesPending = pending is not null && PipeWireFrameSequence.IsNewerThan(generation, pending.StartGeneration);
        var region = completesPending ? pending!.Region : new ScreenRect(0, 0, _width, _height);

        var pwBuffer = Marshal.PtrToStructure<PipeWireBuffer>(bufferPtr);
        var spaBuffer = Marshal.PtrToStructure<SpaBuffer>(pwBuffer.Buffer);
        if (spaBuffer.DataCount == 0 || spaBuffer.Datas == IntPtr.Zero)
        {
            return;
        }

        var data0 = Marshal.PtrToStructure<SpaData>(spaBuffer.Datas);
        if (data0.Data == IntPtr.Zero || data0.Chunk == IntPtr.Zero)
        {
            return;
        }

        var chunk = Marshal.PtrToStructure<SpaChunk>(data0.Chunk);
        if (!PipeWireFrameValidity.IsUsable(spaBuffer, chunk, out _))
        {
            return;
        }

        if (!PipeWireBufferTypePolicy.IsSupported(data0.Type))
        {
            FailCopy(PipeWireBufferTypePolicy.DescribeUnsupported(data0.Type));
            return;
        }

        if (!TryResolveFrameLayout(data0, chunk, out var layout, out var stride, out var offset))
        {
            return;
        }

        if (!completesPending)
        {
            using var cacheUpdate = _frameCache.BeginFullUpdate(generation);
            if (!cacheUpdate.IsAccepted)
            {
                return;
            }

            CopyFramePixelsInto(
                data0,
                layout,
                stride,
                offset,
                region,
                cacheUpdate.Pixels,
                _width,
                _height,
                _width * PipeWireConstants.Xrgb8888BytesPerPixel);
            cacheUpdate.Commit();
            return;
        }

        var framePixels = CopyFramePixels(data0, layout, stride, offset, region);
        var targetStride = checked(region.Width * PipeWireConstants.Xrgb8888BytesPerPixel);
        _frameCache.Update(region, framePixels, targetStride, generation);

        CompletePending(PortalPipeWireFrameResult.Success(new PortalPipeWireFrame(
            new(0, 0, region.Width, region.Height),
            targetStride,
            CrossMacro.Platform.Abstractions.ScreenPixelFormat.Xrgb8888,
            framePixels)));
    }

    private bool TryResolveFrameLayout(SpaData data0, SpaChunk chunk, out PipeWireVideoLayout layout, out int stride, out int offset)
    {
        layout = default;
        offset = 0;
        if (_negotiatedLayout is not { } negotiated)
        {
            stride = 0;
            FailCopy("PipeWire frame arrived before a negotiated video layout was available.");
            return false;
        }

        layout = negotiated;
        stride = 0;
        if (chunk.Stride < 0)
        {
            FailCopy("PipeWire frame reported a negative stride.");
            return false;
        }

        stride = chunk.Stride is 0 ? layout.MinimumStride : chunk.Stride;

        if (stride < layout.MinimumStride)
        {
            FailCopy($"PipeWire frame stride {stride.ToString(CultureInfo.InvariantCulture)} is smaller than the negotiated row width for {layout.Width.ToString(CultureInfo.InvariantCulture)} pixels.");
            return false;
        }

        if (data0.MaxSize == 0)
        {
            offset = 0;
            FailCopy("PipeWire frame data advertised maxsize=0.");
            return false;
        }

        if (chunk.Offset > data0.MaxSize || chunk.Offset > int.MaxValue)
        {
            offset = 0;
            FailCopy($"PipeWire frame chunk offset {chunk.Offset.ToString(CultureInfo.InvariantCulture)} exceeds maxsize {data0.MaxSize.ToString(CultureInfo.InvariantCulture)}.");
            return false;
        }

        offset = checked((int)chunk.Offset);
        var available = data0.MaxSize - chunk.Offset;
        if (chunk.Size > available)
        {
            FailCopy($"PipeWire frame chunk size {chunk.Size.ToString(CultureInfo.InvariantCulture)} exceeds the available buffer range.");
            return false;
        }

        var chunkSize = chunk.Size > 0 ? chunk.Size : available;
        var required = checked(((long)(layout.Height - 1) * stride) + layout.MinimumStride);
        if (chunkSize < required)
        {
            FailCopy($"PipeWire frame chunk is too small for the negotiated frame. offset={offset.ToString(CultureInfo.InvariantCulture)} size={chunk.Size.ToString(CultureInfo.InvariantCulture)} maxsize={data0.MaxSize.ToString(CultureInfo.InvariantCulture)} required={required.ToString(CultureInfo.InvariantCulture)}.");
            return false;
        }

        return true;
    }

    private byte[] CopyFramePixels(SpaData data0, PipeWireVideoLayout layout, int sourceStride, int sourceOffset, ScreenRect region)
    {
        var targetStride = checked(region.Width * PipeWireConstants.Xrgb8888BytesPerPixel);
        var pixels = new byte[checked(targetStride * region.Height)];
        CopyFramePixelsInto(data0, layout, sourceStride, sourceOffset, region, pixels, _width, _height, targetStride);
        return pixels;
    }

    private static void CopyFramePixelsInto(
        SpaData data0,
        PipeWireVideoLayout layout,
        int sourceStride,
        int sourceOffset,
        ScreenRect region,
        Span<byte> pixels,
        int sourceLogicalWidth,
        int sourceLogicalHeight,
        int targetStride)
    {
        var sourceRow = ArrayPool<byte>.Shared.Rent(layout.MinimumStride);
        try
        {
            var previousSourceY = -1;
            for (var row = 0; row < region.Height; row++)
            {
                var sourceY = WaylandLogicalPhysicalMapper.MapPixel(region.Y + row, sourceLogicalHeight, layout.Height);
                if (sourceY != previousSourceY)
                {
                    var sourceRowOffset = checked((int)((long)sourceOffset + ((long)sourceY * sourceStride)));
                    Marshal.Copy(data0.Data + sourceRowOffset, sourceRow, 0, layout.MinimumStride);
                    previousSourceY = sourceY;
                }

                var targetRow = pixels.Slice(row * targetStride, targetStride);
                PipeWireFrameRowConverter.Convert(sourceRow.AsSpan(0, layout.MinimumStride), layout, sourceLogicalWidth, region.X, targetRow);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sourceRow);
        }
    }

    private void HandleNegotiatedFormat(IntPtr parameter)
    {
        if (parameter == IntPtr.Zero)
        {
            return;
        }

        if (!SpaFormatPodParser.TryReadFormat(parameter, out var layout, out var error))
        {
            FailCopy(error);
            return;
        }

        if (_negotiatedLayout is { } previous && previous != layout)
        {
            _frameCache.Clear();
            FailCopy($"PipeWire video format changed from {previous.Width.ToString(CultureInfo.InvariantCulture)}x{previous.Height.ToString(CultureInfo.InvariantCulture)} ({previous.Format}) to {layout.Width.ToString(CultureInfo.InvariantCulture)}x{layout.Height.ToString(CultureInfo.InvariantCulture)} ({layout.Format}).");
            return;
        }

        _negotiatedLayout = layout;
        var bufferParameter = IntPtr.Zero;
        var headerMetadataParameter = IntPtr.Zero;
        var transformMetadataParameter = IntPtr.Zero;
        var damageMetadataParameter = IntPtr.Zero;
        var parameterArray = IntPtr.Zero;
        try
        {
            bufferParameter = SpaFormatPodBuilder.CreateCpuBufferParams(layout.Width, layout.Height, layout.MinimumStride);
            headerMetadataParameter = SpaFormatPodBuilder.CreateMetaParameter(PipeWireConstants.SpaMetaHeader, size: 32);
            transformMetadataParameter = SpaFormatPodBuilder.CreateMetaParameter(PipeWireConstants.SpaMetaVideoTransform, size: 4);
            damageMetadataParameter = SpaFormatPodBuilder.CreateMetaParameter(PipeWireConstants.SpaMetaVideoDamage, size: 64, minimumSize: 16, maximumSize: 64);
            const int parameterCount = 4;
            parameterArray = Marshal.AllocHGlobal(IntPtr.Size * parameterCount);
            Marshal.WriteIntPtr(parameterArray, bufferParameter);
            Marshal.WriteIntPtr(parameterArray + IntPtr.Size, headerMetadataParameter);
            Marshal.WriteIntPtr(parameterArray + (IntPtr.Size * 2), transformMetadataParameter);
            Marshal.WriteIntPtr(parameterArray + (IntPtr.Size * 3), damageMetadataParameter);
            var result = _lib.StreamUpdateParams(_stream, parameterArray, parameterCount);
            if (result < 0)
            {
                FailCopy($"pw_stream_update_params failed rc={result.ToString(CultureInfo.InvariantCulture)}.");
            }
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException or OutOfMemoryException)
        {
            FailCopy($"PipeWire buffer negotiation could not be prepared: {ex.Message}");
        }
        finally
        {
            if (bufferParameter != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(bufferParameter);
            }

            if (headerMetadataParameter != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(headerMetadataParameter);
            }

            if (transformMetadataParameter != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(transformMetadataParameter);
            }

            if (damageMetadataParameter != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(damageMetadataParameter);
            }

            if (parameterArray != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(parameterArray);
            }
        }
    }

    private void FailCopy(string message)
    {
        _error = message;
        _frameCache.Clear();
        CompletePending(PortalPipeWireFrameResult.Failure(ScreenReadErrorKind.CaptureFailed, message));
        _lib.ThreadLoopSignal(_threadLoop, waitForAccept: false);
    }

    private static PortalPipeWireFrameCapture FromHandle(IntPtr data) =>
        (PortalPipeWireFrameCapture)(GCHandle.FromIntPtr(data).Target ?? throw new InvalidOperationException("PipeWire callback target was released."));
}
