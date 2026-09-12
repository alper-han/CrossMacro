namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class SwayIpcClientTests
{
    [Fact]
    public async Task SendRequestAsync_ShouldTransmitCompleteLargeBinaryFrameBeforeReadingResponse()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-sway");
        var payload = $"{{\"command\":\"{new string('x', 1024 * 1024)}\"}}";
        const uint requestType = 42;
        const string responsePayload = "{\"success\":true}";

        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(socketPath));
        listener.Listen(1);

        var serverTask = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptAsync(CancellationToken.None);
            var header = new byte[14];
            await ReadExactAsync(accepted, header, CancellationToken.None);

            Assert.Equal("i3-ipc", Encoding.ASCII.GetString(header, 0, 6));
            var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(6, 4));
            Assert.Equal((uint)Encoding.UTF8.GetByteCount(payload), payloadLength);
            Assert.Equal(requestType, BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(10, 4)));

            var requestBytes = new byte[payloadLength];
            await ReadExactAsync(accepted, requestBytes, CancellationToken.None);
            Assert.Equal(payload, Encoding.UTF8.GetString(requestBytes));

            var responseBytes = Encoding.UTF8.GetBytes(responsePayload);
            var responseHeader = new byte[14];
            Encoding.ASCII.GetBytes("i3-ipc").CopyTo(responseHeader, 0);
            BinaryPrimitives.WriteUInt32LittleEndian(responseHeader.AsSpan(6, 4), (uint)responseBytes.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(responseHeader.AsSpan(10, 4), 0);
            await WriteAllAsync(accepted, responseHeader, CancellationToken.None);
            await WriteAllAsync(accepted, responseBytes, CancellationToken.None);
        }, CancellationToken.None);

        try
        {
            using var client = new SwayIpcClient(socketPath);

            var response = await client.SendRequestAsync(requestType, payload, CancellationToken.None);

            Assert.Equal(responsePayload, response);
            await serverTask.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
        }
        finally
        {
            listener.Close();
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }

    private static async Task ReadExactAsync(Socket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        int readTotal = 0;
        while (readTotal < buffer.Length)
        {
            int read = await socket.ReceiveAsync(buffer.AsMemory(readTotal), SocketFlags.None, cancellationToken);
            if (read <= 0)
            {
                throw new IOException("Unexpected EOF.");
            }

            readTotal += read;
        }
    }

    private static async Task WriteAllAsync(Socket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        int writtenTotal = 0;
        while (writtenTotal < buffer.Length)
        {
            int written = await socket.SendAsync(buffer.AsMemory(writtenTotal), SocketFlags.None, cancellationToken);
            if (written <= 0)
            {
                throw new IOException("Failed to write response.");
            }

            writtenTotal += written;
        }
    }
}
