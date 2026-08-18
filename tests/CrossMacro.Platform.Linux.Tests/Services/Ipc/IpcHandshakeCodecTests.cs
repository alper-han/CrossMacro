
namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class IpcHandshakeCodecTests
{
    [Fact]
    public async Task ReadStringAsync_ReadsPartialHandshakePayload()
    {
        var payload = new MemoryStream(Encoding.UTF8.GetBytes("\x05hello"));

        var result = await IpcHandshakeCodec.ReadStringAsync(payload, CancellationToken.None);

        Assert.Equal("hello", result);
    }

    [Fact]
    public async Task ReadInt32Async_ReadsProtocolVersionPayload()
    {
        var payload = new MemoryStream(BitConverter.GetBytes(42));

        var result = await IpcHandshakeCodec.ReadInt32Async(payload, CancellationToken.None);

        Assert.Equal(42, result);
    }
}
