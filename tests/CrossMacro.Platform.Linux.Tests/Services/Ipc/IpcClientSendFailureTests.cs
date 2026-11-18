namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

public sealed class IpcClientSendFailureTests
{
    private static readonly TimeSpan AsyncOperationTimeout = TimeSpan.FromSeconds(5);

    [LinuxFact]
    public async Task ErrorFromDaemon_WhenOneHandlerThrows_OtherHandlersStillRun()
    {
        var socketPath = TestSocketPaths.CreateShort("cm-ipc");
        await using var daemon = await TestIpcDaemon.StartAsync(socketPath);
        using var client = new IpcClient(() => socketPath, autoReconnect: false);
        var healthyHandlerMessage = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ErrorOccurred += static (_, _) => throw new InvalidOperationException("Simulated error callback failure");
        client.ErrorOccurred += (_, args) => healthyHandlerMessage.TrySetResult(args.Message);
        await client.ConnectAsync(CancellationToken.None);

        using var frame = new MemoryStream();
        using (var writer = new BinaryWriter(frame, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            IpcMessageCodec.WriteError(writer, "daemon exploded");
        }

        await daemon.WriteToClientAsync(frame.ToArray(), CancellationToken.None);

        var message = await healthyHandlerMessage.Task.WaitAsync(AsyncOperationTimeout, TimeProvider.System, CancellationToken.None);
        Assert.Equal("daemon exploded", message);
        Assert.True(client.IsConnected);
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenCalledConcurrently_BothCallersCompleteTheSharedCleanup()
    {
        using var client = new IpcClient(() => "/tmp/non-existent.sock", autoReconnect: false);

        var firstDispose = DisposeClientAsync(client);
        var secondDispose = DisposeClientAsync(client);

        await Task.WhenAll(firstDispose, secondDispose).WaitAsync(
            AsyncOperationTimeout,
            TimeProvider.System,
            CancellationToken.None);

        Assert.True(firstDispose.IsCompletedSuccessfully);
        Assert.True(secondDispose.IsCompletedSuccessfully);
        Assert.False(client.IsConnected);
    }

    private static async Task DisposeClientAsync(IpcClient client)
    {
        await client.DisposeAsync();
    }
}
