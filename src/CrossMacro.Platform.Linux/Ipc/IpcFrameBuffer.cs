namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Reusable serialization buffer so a frame leaves the process in one socket write instead of
/// one write per field. Not thread-safe: callers serialize access with the transport write gate.
/// </summary>
internal sealed class IpcFrameBuffer : IDisposable
{
    private readonly MemoryStream _buffer = new();
    private readonly BinaryWriter _writer;

    public IpcFrameBuffer()
    {
        _writer = new BinaryWriter(_buffer);
    }

    /// <summary>Serializes one frame and returns a view that stays valid until the next call.</summary>
    public ReadOnlyMemory<byte> Build(IpcOpCode op, Action<BinaryWriter>? write, bool prefixOpCode)
    {
        _buffer.SetLength(0);
        if (prefixOpCode)
        {
            _writer.Write((byte)op);
        }

        write?.Invoke(_writer);
        return new ReadOnlyMemory<byte>(_buffer.GetBuffer(), 0, checked((int)_buffer.Length));
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
