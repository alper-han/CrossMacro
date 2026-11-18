using System.Buffers.Binary;

namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class IpcHandshakeExchangeTests
{
    [Fact]
    public async Task PerformAsync_SendsHandshakeRequestAndAcceptsMatchingVersion()
    {
        using var stream = new ScriptedStream(HandshakeResponse(IpcProtocol.ProtocolVersion));

        await IpcHandshakeExchange.PerformAsync(stream, CancellationToken.None);

        Assert.Equal(IpcHandshakeWireCodec.CreateHandshake(), stream.Written.ToArray());
    }

    [Fact]
    public async Task PerformAsync_WhenVersionDiffers_ThrowsProtocolMismatch()
    {
        using var stream = new ScriptedStream(HandshakeResponse(IpcProtocol.ProtocolVersion + 1));

        var exception = await Assert.ThrowsAsync<IpcClientException>(
            () => IpcHandshakeExchange.PerformAsync(stream, CancellationToken.None));

        Assert.Equal(IpcClientFailureReason.ProtocolMismatch, exception.Reason);
    }

    [Fact]
    public async Task PerformAsync_WhenDaemonReportsError_ThrowsHandshakeFailedWithDaemonMessage()
    {
        using var stream = new ScriptedStream([(byte)IpcOpCode.Error, 0x04, .. "busy"u8.ToArray()]);

        var exception = await Assert.ThrowsAsync<IpcClientException>(
            () => IpcHandshakeExchange.PerformAsync(stream, CancellationToken.None));

        Assert.Equal(IpcClientFailureReason.HandshakeFailed, exception.Reason);
        Assert.Contains("busy", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PerformAsync_WhenFirstByteIsNotHandshake_ThrowsHandshakeFailed()
    {
        using var stream = new ScriptedStream([0xFE]);

        var exception = await Assert.ThrowsAsync<IpcClientException>(
            () => IpcHandshakeExchange.PerformAsync(stream, CancellationToken.None));

        Assert.Equal(IpcClientFailureReason.HandshakeFailed, exception.Reason);
    }

    [Fact]
    public async Task PerformAsync_WhenDaemonClosesMidResponse_ThrowsEndOfStream()
    {
        using var stream = new ScriptedStream([(byte)IpcOpCode.Handshake, 0x01]);

        _ = await Assert.ThrowsAsync<EndOfStreamException>(
            () => IpcHandshakeExchange.PerformAsync(stream, CancellationToken.None));
    }

    private static byte[] HandshakeResponse(int version)
    {
        var response = new byte[1 + sizeof(int)];
        response[0] = (byte)IpcOpCode.Handshake;
        BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(1), version);
        return response;
    }

    private sealed class ScriptedStream(byte[] response) : Stream
    {
        private readonly MemoryStream _response = new(response);

        public MemoryStream Written { get; } = new();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _response.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _response.ReadAsync(buffer, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) => Written.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            Written.WriteAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _response.Dispose();
                Written.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
