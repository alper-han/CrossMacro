using System.Text;

namespace CrossMacro.Daemon.Tests.Services;

public sealed class IpcWireCodecTests
{
    [Fact]
    public void Handshake_VersionFour_HasStableLittleEndianBytes()
    {
        Assert.Equal(new byte[] { 1, 4, 0, 0, 0 }, IpcHandshakeWireCodec.CreateRequest());
    }

    [Fact]
    public void InputEvent_UsesV4FieldOrderAndSignedValues()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var value = new IpcInputEvent { Type = 3, Code = 0x1234, Value = -2, Timestamp = 0x0102030405060708 };
        IpcMessageCodec.WriteInputEvent(writer, value);

        Assert.Equal(new byte[] { 4, 3, 0x34, 0x12, 0, 0, 0xfe, 0xff, 0xff, 0xff, 8, 7, 6, 5, 4, 3, 2, 1 }, stream.ToArray());
        stream.Position = 1;
        using var reader = new BinaryReader(stream);
        Assert.Equal(value, IpcMessageCodec.ReadInputEventPayload(reader));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(IpcProtocol.MaxSimulationBatchEvents + 1)]
    public void SimulationBatch_RejectsInvalidCountBeforeReadingEvents(int count)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(count);
        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        _ = Assert.Throws<InvalidDataException>(() => IpcMessageCodec.ReadSimulationBatchPayload(reader));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    [InlineData(IpcProtocol.MaxSimulationBatchEvents + 1, false)]
    public void SimulationBatch_CountFailureReportsWhetherFrameBoundaryIsKnown(int count, bool hasCompleteFrame)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(count);
        stream.Position = 0;
        using var reader = new BinaryReader(stream);

        var result = IpcMessageCodec.ReadSimulationBatch(reader);

        Assert.False(result.Success);
        Assert.Equal(hasCompleteFrame, result.HasCompleteFrame);
        Assert.Empty(result.Events);
        Assert.Contains("event count", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandshakeReaders_HandleFragmentationAndAgreeOnUtf8Payload()
    {
        using var encoded = new MemoryStream();
        using (var writer = new BinaryWriter(encoded, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("İzin reddedildi");
        }
        using var synchronous = new FragmentedStream(encoded.ToArray());
        using var asynchronous = new FragmentedStream(encoded.ToArray());

        Assert.Equal("İzin reddedildi", IpcHandshakeWireCodec.ReadString(synchronous));
        Assert.Equal("İzin reddedildi", await IpcHandshakeWireCodec.ReadStringAsync(asynchronous, CancellationToken.None));
    }

    [Fact]
    public async Task HandshakeReaders_RejectTruncatedPayload()
    {
        using var synchronous = new FragmentedStream([5, (byte)'x']);
        using var asynchronous = new FragmentedStream([5, (byte)'x']);
        _ = Assert.Throws<EndOfStreamException>(() => IpcHandshakeWireCodec.ReadString(synchronous));
        _ = await Assert.ThrowsAsync<EndOfStreamException>(() => IpcHandshakeWireCodec.ReadStringAsync(asynchronous, CancellationToken.None));
    }

    [Theory]
    [InlineData(0x08, typeof(IOException))]
    [InlineData(0x80, typeof(FormatException))]
    public async Task HandshakeReaders_RejectMalformedSevenBitLength(byte lastByte, Type expectedException)
    {
        using var synchronous = new FragmentedStream([0xff, 0xff, 0xff, 0xff, lastByte]);
        using var asynchronous = new FragmentedStream([0xff, 0xff, 0xff, 0xff, lastByte]);
        _ = Assert.Throws(expectedException, () => IpcHandshakeWireCodec.ReadString(synchronous));
        _ = await Assert.ThrowsAsync(expectedException, () => IpcHandshakeWireCodec.ReadStringAsync(asynchronous, CancellationToken.None));
    }

    [Fact]
    public void SimulationBatch_AndAcknowledgementRoundTripPreserveEventCount()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        var input = new IpcSimulationRequest { Type = 2, Code = 8, Value = -120, DelayAfterMicroseconds = 1234 };
        IpcMessageCodec.WriteSimulationBatchHeader(writer, 0x12345678, 1);
        IpcMessageCodec.WriteSimulationEvent(writer, input);
        Assert.Equal(new byte[] { 9, 0x78, 0x56, 0x34, 0x12, 1, 0, 0, 0 }, stream.ToArray()[..9]);
        stream.Position = 1;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        Assert.Equal(0x12345678, IpcMessageCodec.ReadRequestId(reader));
        var events = IpcMessageCodec.ReadSimulationBatchPayload(reader);
        var decoded = Assert.Single(events);
        Assert.Equal(input, decoded);
        stream.SetLength(0);
        stream.Position = 0;
        IpcMessageCodec.WriteSimulationBatchCompleted(writer, 0x12345678, events.Length);
        stream.Position = 1;
        Assert.Equal((0x12345678, 1), IpcMessageCodec.ReadSimulationBatchCompletedPayload(reader));
    }

    private sealed class FragmentedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(1, buffer.Length)]);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
