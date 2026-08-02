namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class X11ClipboardOwner : IDisposable
{
    private const int EventBufferSize = 192;
    private const int IncrementalTransferThreshold = 64 * 1024;
    private const int IncrementalChunkSize = 64 * 1024;

    private readonly IntPtr _display;
    private readonly nuint _window;
    private readonly X11ClipboardAtoms _atoms;
    private readonly byte[] _data;
    private readonly X11ClipboardDataKind _dataKind;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _eventLoop;
    private readonly Dictionary<(nuint Requestor, nuint Property), X11IncrementalTransfer> _transfers = [];
    private bool _disposed;

    private X11ClipboardOwner(
        IntPtr display,
        nuint window,
        X11ClipboardAtoms atoms,
        byte[] data,
        X11ClipboardDataKind dataKind)
    {
        _display = display;
        _window = window;
        _atoms = atoms;
        _data = data;
        _dataKind = dataKind;
        _eventLoop = Task.Run(EventLoop, _shutdown.Token);
    }

    public static X11ClipboardOwner Create(byte[] data, X11ClipboardDataKind dataKind, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var display = X11Native.XOpenDisplay(display: null);
        if (display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to open the X11 display for clipboard ownership.");
        }

        nuint window = 0;
        try
        {
            var atoms = X11ClipboardAtoms.Create(display);
            var root = (nuint)X11Native.XDefaultRootWindow(display);
            window = X11Native.XCreateSimpleWindow(display, root, 0, 0, 1, 1, 0, 0, 0);
            if (window is 0)
            {
                throw new InvalidOperationException("Failed to create the X11 clipboard owner window.");
            }

            _ = X11Native.XSelectInput(display, window, X11Native.PropertyChangeMask);
            _ = X11Native.XSetSelectionOwner(display, atoms.Clipboard, window, X11Native.CurrentTime);
            if (X11Native.XGetSelectionOwner(display, atoms.Clipboard) != window)
            {
                throw new InvalidOperationException("X11 refused clipboard ownership.");
            }

            _ = X11Native.XFlush(display);
            return new X11ClipboardOwner(display, window, atoms, data, dataKind);
        }
        catch
        {
            if (window is not 0)
            {
                _ = X11Native.XDestroyWindow(display, window);
            }

            _ = X11Native.XCloseDisplay(display);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        try
        {
            _eventLoop.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            Log.Debug(ex, "[X11NativeClipboard] Clipboard owner event loop stopped during disposal");
        }

        _shutdown.Dispose();
        _ = X11Native.XDestroyWindow(_display, _window);
        _ = X11Native.XCloseDisplay(_display);
    }

    private void EventLoop()
    {
        var eventMemory = Marshal.AllocHGlobal(EventBufferSize);
        try
        {
            var connection = X11Native.XConnectionNumber(_display);
            while (!_shutdown.IsCancellationRequested)
            {
                if (!LinuxClipboardNative.WaitForReadable(connection, _shutdown.Token))
                {
                    continue;
                }

                while (X11Native.XPending(_display) > 0)
                {
                    _shutdown.Token.ThrowIfCancellationRequested();
                    _ = X11Native.XNextEvent(_display, eventMemory);
                    HandleEvent(eventMemory);
                }

            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Normal owner shutdown.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[X11NativeClipboard] Clipboard owner event loop failed");
        }
        finally
        {
            Marshal.FreeHGlobal(eventMemory);
        }
    }

    private void HandleEvent(IntPtr eventMemory)
    {
        var type = Marshal.ReadInt32(eventMemory);
        switch (type)
        {
            case X11Native.SelectionRequest:
                HandleSelectionRequest(Marshal.PtrToStructure<XSelectionRequestEvent>(eventMemory));
                break;
            case X11Native.PropertyNotify:
                HandlePropertyNotify(Marshal.PtrToStructure<XPropertyEvent>(eventMemory));
                break;
            case X11Native.SelectionClear:
                var selectionClear = Marshal.PtrToStructure<XSelectionClearEvent>(eventMemory);
                if (selectionClear.Selection == _atoms.Clipboard)
                {
                    _shutdown.Cancel();
                }

                break;
        }
    }

    private void HandleSelectionRequest(XSelectionRequestEvent request)
    {
        var property = request.Property is 0 ? request.Target : request.Property;
        if (property is 0)
        {
            SendSelectionNotify(request, 0);
            return;
        }

        if (request.Target == _atoms.Targets)
        {
            nuint[] targets = _dataKind is X11ClipboardDataKind.Text
                ? [
                    _atoms.Targets,
                    _atoms.Utf8String,
                    _atoms.TextPlainUtf8,
                    _atoms.TextPlain,
                    _atoms.Text,
                    _atoms.String,
                ]
                : [
                    _atoms.Targets,
                    _atoms.ImagePng,
                ];
            ChangeAtoms(request.Requestor, property, _atoms.Atom, targets);
            SendSelectionNotify(request, property);
            return;
        }

        if (!TryGetTargetData(request.Target, out var data, out var type))
        {
            SendSelectionNotify(request, 0);
            return;
        }

        _ = X11Native.XSelectInput(_display, request.Requestor, X11Native.PropertyChangeMask);
        if (data.Length > IncrementalTransferThreshold)
        {
            var key = (request.Requestor, property);
            _transfers[key] = new X11IncrementalTransfer(request.Requestor, property, type, data);
            ChangeAtoms(request.Requestor, property, _atoms.Incr, [(nuint)data.Length]);
        }
        else
        {
            ChangeBytes(request.Requestor, property, type, data);
        }

        SendSelectionNotify(request, property);
        _ = X11Native.XFlush(_display);
    }

    private void HandlePropertyNotify(XPropertyEvent property)
    {
        if (property.State is not X11Native.PropertyDelete)
        {
            return;
        }

        var key = (property.Window, property.Atom);
        if (!_transfers.TryGetValue(key, out var transfer))
        {
            return;
        }

        if (transfer.Offset < transfer.Data.Length)
        {
            var length = Math.Min(IncrementalChunkSize, transfer.Data.Length - transfer.Offset);
            var chunk = transfer.Data.AsSpan(transfer.Offset, length).ToArray();
            ChangeBytes(transfer.Requestor, transfer.Property, transfer.Type, chunk);
            transfer.Offset += length;
            return;
        }

        ChangeBytes(transfer.Requestor, transfer.Property, transfer.Type, []);
        _ = _transfers.Remove(key);
        _ = X11Native.XFlush(_display);
    }

    private bool TryGetTargetData(nuint target, out byte[] data, out nuint type)
    {
        if (_dataKind is X11ClipboardDataKind.Png)
        {
            data = target == _atoms.ImagePng ? _data : [];
            type = _atoms.ImagePng;
            return target == _atoms.ImagePng;
        }

        if (target is not 0 &&
            target != _atoms.Utf8String &&
            target != _atoms.TextPlainUtf8 &&
            target != _atoms.TextPlain &&
            target != _atoms.Text &&
            target != _atoms.String)
        {
            data = [];
            type = 0;
            return false;
        }

        if (target == _atoms.String)
        {
            data = Encoding.Latin1.GetBytes(Encoding.UTF8.GetString(_data));
            type = _atoms.String;
        }
        else
        {
            data = _data;
            type = target == _atoms.Utf8String ? _atoms.Utf8String : target;
        }

        return true;
    }

    private void SendSelectionNotify(XSelectionRequestEvent request, nuint property)
    {
        var notify = new XSelectionEvent
        {
            Type = X11Native.SelectionNotify,
            Serial = 0,
            SendEvent = 1,
            Display = _display,
            Requestor = request.Requestor,
            Selection = request.Selection,
            Target = request.Target,
            Property = property,
            Time = request.Time,
        };
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<XSelectionEvent>());
        try
        {
            Marshal.StructureToPtr(notify, memory, fDeleteOld: false);
            _ = X11Native.XSendEvent(_display, request.Requestor, propagate: false, event_mask: 0, event_send: memory);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private void ChangeAtoms(nuint window, nuint property, nuint type, ReadOnlySpan<nuint> atoms)
    {
        var values = atoms.ToArray();
        if (values.Length is 0)
        {
            _ = X11Native.XChangeProperty(_display, window, property, type, 32, X11Native.PropModeReplace, IntPtr.Zero, 0);
            return;
        }

        var handle = GCHandle.Alloc(values, GCHandleType.Pinned);
        try
        {
            _ = X11Native.XChangeProperty(_display, window, property, type, 32, X11Native.PropModeReplace, handle.AddrOfPinnedObject(), values.Length);
        }
        finally
        {
            handle.Free();
        }
    }

    private void ChangeBytes(nuint window, nuint property, nuint type, ReadOnlySpan<byte> data)
    {
        if (data.Length is 0)
        {
            _ = X11Native.XChangeProperty(_display, window, property, type, 8, X11Native.PropModeReplace, IntPtr.Zero, 0);
            return;
        }

        var values = data.ToArray();
        var handle = GCHandle.Alloc(values, GCHandleType.Pinned);
        try
        {
            _ = X11Native.XChangeProperty(_display, window, property, type, 8, X11Native.PropModeReplace, handle.AddrOfPinnedObject(), values.Length);
        }
        finally
        {
            handle.Free();
        }
    }
}
