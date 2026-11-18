namespace CrossMacro.Platform.Linux.Clipboard;

internal sealed class X11ClipboardReader
{
    private const int EventBufferSize = 192;
    private const int PollSliceMilliseconds = 100;

    /// <summary>Reply size per request, in 32-bit units (1 MiB).</summary>
    private const int PropertyReadUnits = 256 * 1024;

    /// <summary>How long the selection owner may stay silent before the read gives up.</summary>
    private static readonly TimeSpan OwnerInactivityTimeout = TimeSpan.FromSeconds(5);

    private readonly IntPtr _display;
    private readonly nuint _window;
    private readonly X11ClipboardAtoms _atoms;
    private long _lastActivityTimestamp = TimeProvider.System.GetTimestamp();

    private X11ClipboardReader(IntPtr display, nuint window, X11ClipboardAtoms atoms)
    {
        _display = display;
        _window = window;
        _atoms = atoms;
    }

    public static string? ReadText(CancellationToken cancellationToken)
    {
        var display = X11Native.XOpenDisplay(display: null);
        if (display == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to open the X11 display for clipboard reading.");
        }

        nuint window = 0;
        try
        {
            var atoms = X11ClipboardAtoms.Create(display);
            window = X11Native.XCreateSimpleWindow(display, (nuint)X11Native.XDefaultRootWindow(display), 0, 0, 1, 1, 0, 0, 0);
            if (window is 0)
            {
                throw new InvalidOperationException("Failed to create the X11 clipboard requestor window.");
            }

            _ = X11Native.XSelectInput(display, window, X11Native.PropertyChangeMask);
            if (X11Native.XGetSelectionOwner(display, atoms.Clipboard) is 0)
            {
                return string.Empty;
            }

            var reader = new X11ClipboardReader(display, window, atoms);
            try
            {
                foreach (var target in new[] { atoms.Utf8String, atoms.TextPlainUtf8, atoms.TextPlain, atoms.Text, atoms.String })
                {
                    var bytes = reader.Request(target, cancellationToken);
                    if (bytes is null)
                    {
                        continue;
                    }

                    return target == atoms.String
                        ? Encoding.Latin1.GetString(bytes)
                        : Encoding.UTF8.GetString(bytes);
                }
            }
            catch (TimeoutException ex)
            {
                Log.Warning(ex, "[X11ClipboardReader] The clipboard owner stopped answering; returning no text");
            }

            return string.Empty;
        }
        finally
        {
            if (window is not 0)
            {
                _ = X11Native.XDestroyWindow(display, window);
            }

            _ = X11Native.XCloseDisplay(display);
        }
    }

    private byte[]? Request(nuint target, CancellationToken cancellationToken)
    {
        _lastActivityTimestamp = TimeProvider.System.GetTimestamp();
        _ = X11Native.XDeleteProperty(_display, _window, _atoms.Property);
        _ = X11Native.XConvertSelection(_display, _atoms.Clipboard, target, _atoms.Property, _window, X11Native.CurrentTime);
        _ = X11Native.XFlush(_display);

        var eventMemory = Marshal.AllocHGlobal(EventBufferSize);
        try
        {
            var connection = X11Native.XConnectionNumber(_display);
            while (true)
            {
                WaitForServerData(connection, cancellationToken);
                while (X11Native.XPending(_display) > 0)
                {
                    _ = X11Native.XNextEvent(_display, eventMemory);
                    var type = Marshal.ReadInt32(eventMemory);
                    if (type == X11Native.SelectionNotify)
                    {
                        var notify = Marshal.PtrToStructure<XSelectionEvent>(eventMemory);
                        if (notify.Requestor == _window && notify.Selection == _atoms.Clipboard && notify.Target == target)
                        {
                            return notify.Property is 0 ? null : ReadPropertyTransfer(cancellationToken);
                        }
                    }
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(eventMemory);
        }
    }

    private byte[] ReadPropertyTransfer(CancellationToken cancellationToken)
    {
        var first = ReadProperty(delete: true);
        if (first.Type != _atoms.Incr)
        {
            return first.Data;
        }

        var result = new ArrayBufferWriter<byte>();
        var eventMemory = Marshal.AllocHGlobal(EventBufferSize);
        try
        {
            while (true)
            {
                WaitForServerData(X11Native.XConnectionNumber(_display), cancellationToken);
                while (X11Native.XPending(_display) > 0)
                {
                    _ = X11Native.XNextEvent(_display, eventMemory);
                    if (Marshal.ReadInt32(eventMemory) != X11Native.PropertyNotify)
                    {
                        continue;
                    }

                    var property = Marshal.PtrToStructure<XPropertyEvent>(eventMemory);
                    if (property.Window != _window || property.Atom != _atoms.Property || property.State != X11Native.PropertyNewValue)
                    {
                        continue;
                    }

                    _lastActivityTimestamp = TimeProvider.System.GetTimestamp();
                    var chunk = ReadProperty(delete: true);
                    if (chunk.Data.Length is 0)
                    {
                        return result.WrittenSpan.ToArray();
                    }

                    result.Write(chunk.Data);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(eventMemory);
        }
    }

    /// <summary>
    /// Blocks until the X connection has data. The wait is an inactivity deadline: every request or
    /// transfer chunk restarts it, so a large INCR transfer is fine but a silent owner is not.
    /// </summary>
    private void WaitForServerData(int connection, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = OwnerInactivityTimeout - TimeProvider.System.GetElapsedTime(_lastActivityTimestamp);
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("The X11 clipboard owner did not answer in time.");
            }

            var slice = (int)Math.Min(remaining.TotalMilliseconds, PollSliceMilliseconds);
            if (LinuxFileDescriptorNative.PollReadable(connection, Math.Max(1, slice)))
            {
                return;
            }
        }
    }

    private X11PropertyData ReadProperty(bool delete) =>
        X11PropertyAssembler.ReadAll(offsetUnits => ReadPropertyChunk(offsetUnits, delete));

    private X11PropertyChunk ReadPropertyChunk(long offsetUnits, bool delete)
    {
        // With delete set the server only removes the property once the final part has been read.
        var status = X11Native.XGetWindowProperty(
            _display,
            _window,
            _atoms.Property,
            (nint)offsetUnits,
            PropertyReadUnits,
            delete,
            0,
            out var actualType,
            out var actualFormat,
            out var itemCount,
            out var bytesAfter,
            out var property);
        if (status is not 0)
        {
            throw new InvalidOperationException($"XGetWindowProperty failed with status {status.ToString(CultureInfo.InvariantCulture)}.");
        }

        try
        {
            if (property == IntPtr.Zero || itemCount is 0 || actualFormat is not (8 or 16 or 32))
            {
                return new X11PropertyChunk(actualType, [], WireBytesRead: 0, BytesAfter: 0);
            }

            var data = new byte[checked((int)(itemCount * (nuint)X11PropertyAssembler.ItemBytes(actualFormat)))];
            Marshal.Copy(property, data, 0, data.Length);
            return new X11PropertyChunk(
                actualType,
                data,
                WireBytesRead: checked((long)itemCount * (actualFormat / 8)),
                BytesAfter: checked((long)bytesAfter));
        }
        finally
        {
            if (property != IntPtr.Zero)
            {
                _ = X11Native.XFree(property);
            }
        }
    }
}
