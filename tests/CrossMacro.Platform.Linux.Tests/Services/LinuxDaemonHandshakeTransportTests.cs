
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxDaemonHandshakeTransportTests : IDisposable
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"cm-hs-{Guid.NewGuid():N}");

    public LinuxDaemonHandshakeTransportTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task ProbeAsync_WhenDaemonAnswersWithCurrentVersion_Succeeds()
    {
        using var server = StartServer(HandshakeResponse(IpcProtocol.ProtocolVersion));

        var result = await LinuxDaemonHandshakeTransport.ProbeAsync(server.Path, Budget, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(server.Path, result.SocketPath);
        Assert.Equal(IpcHandshakeWireCodec.CreateHandshake(), await server.Request);
    }

    [Fact]
    public async Task ProbeAsync_WhenDaemonSpeaksAnotherVersion_ReportsProtocolMismatch()
    {
        using var server = StartServer(HandshakeResponse(IpcProtocol.ProtocolVersion + 1));

        var result = await LinuxDaemonHandshakeTransport.ProbeAsync(server.Path, Budget, TestContext.Current.CancellationToken);

        Assert.Equal(LinuxDaemonHandshakeStatus.ProtocolMismatch, result.Status);
    }

    [Fact]
    public async Task ProbeAsync_WhenDaemonRejects_ReportsHandshakeRejectedWithDaemonMessage()
    {
        using var server = StartServer([(byte)IpcOpCode.Error, 0x04, .. "busy"u8.ToArray()]);

        var result = await LinuxDaemonHandshakeTransport.ProbeAsync(server.Path, Budget, TestContext.Current.CancellationToken);

        Assert.Equal(LinuxDaemonHandshakeStatus.HandshakeRejected, result.Status);
        Assert.Contains("busy", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbeAsync_WhenDaemonNeverAnswers_ReportsTimeoutWithinBudget()
    {
        using var server = StartServer(response: null);
        var stopwatch = Stopwatch.StartNew();

        var result = await LinuxDaemonHandshakeTransport.ProbeAsync(
            server.Path, TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        Assert.True(result.TimedOut);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task ProbeAsync_WhenNothingListens_ReportsAFailureStatus()
    {
        var path = Path.Combine(_directory, "missing.sock");

        var result = await LinuxDaemonHandshakeTransport.ProbeAsync(path, Budget, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotEqual(LinuxDaemonHandshakeStatus.Success, result.Status);
    }

    [Fact]
    public async Task ProbeAsync_PropagatesCallerCancellation()
    {
        using var server = StartServer(response: null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => LinuxDaemonHandshakeTransport.ProbeAsync(server.Path, Budget, cancellation.Token));
    }

    [Fact]
    public void MapFailure_ClassifiesTheFailureThatCausedTheProbeToFail()
    {
        Assert.Equal(LinuxDaemonHandshakeStatus.UnexpectedError, LinuxDaemonHandshakeTransport.MapFailure(failure: null));
        Assert.Equal(LinuxDaemonHandshakeStatus.PermissionDenied, LinuxDaemonHandshakeTransport.MapFailure(new UnauthorizedAccessException()));
        Assert.Equal(LinuxDaemonHandshakeStatus.PermissionDenied, LinuxDaemonHandshakeTransport.MapFailure(new SocketException((int)SocketError.AccessDenied)));
        Assert.Equal(LinuxDaemonHandshakeStatus.Timeout, LinuxDaemonHandshakeTransport.MapFailure(new TimeoutException()));
        Assert.Equal(LinuxDaemonHandshakeStatus.Timeout, LinuxDaemonHandshakeTransport.MapFailure(new IOException("read", new SocketException((int)SocketError.TimedOut))));
        Assert.Equal(LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale, LinuxDaemonHandshakeTransport.MapFailure(new SocketException((int)SocketError.ConnectionRefused)));
        Assert.Equal(LinuxDaemonHandshakeStatus.MissingSocket, LinuxDaemonHandshakeTransport.MapFailure(new FileNotFoundException()));
        Assert.Equal(LinuxDaemonHandshakeStatus.WrongSocketType, LinuxDaemonHandshakeTransport.MapFailure(new IOException("Endpoint is not a socket.")));
        Assert.Equal(LinuxDaemonHandshakeStatus.UnexpectedError, LinuxDaemonHandshakeTransport.MapFailure(new InvalidOperationException()));
    }

    [Theory]
    [InlineData(IpcClientFailureReason.SocketNotFound, LinuxDaemonHandshakeStatus.MissingSocket)]
    [InlineData(IpcClientFailureReason.ConnectFailed, LinuxDaemonHandshakeStatus.ConnectionRefusedOrStale)]
    [InlineData(IpcClientFailureReason.PermissionDenied, LinuxDaemonHandshakeStatus.PermissionDenied)]
    [InlineData(IpcClientFailureReason.HandshakeFailed, LinuxDaemonHandshakeStatus.HandshakeRejected)]
    [InlineData(IpcClientFailureReason.ProtocolMismatch, LinuxDaemonHandshakeStatus.ProtocolMismatch)]
    [InlineData(IpcClientFailureReason.Timeout, LinuxDaemonHandshakeStatus.Timeout)]
    public void MapFailure_MapsIpcClientFailureReasons(IpcClientFailureReason reason, LinuxDaemonHandshakeStatus expected)
    {
        Assert.Equal(expected, LinuxDaemonHandshakeTransport.MapFailure(new IpcClientException(reason, "failed")));
    }

    private static byte[] HandshakeResponse(int version)
    {
        var response = new byte[1 + sizeof(int)];
        response[0] = (byte)IpcOpCode.Handshake;
        BinaryPrimitives.WriteInt32LittleEndian(response.AsSpan(1), version);
        return response;
    }

    private FakeDaemon StartServer(byte[]? response) => new(Path.Combine(_directory, "daemon.sock"), response);

    private sealed class FakeDaemon : IDisposable
    {
        private readonly Socket _listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        private readonly CancellationTokenSource _stop = new();

        public FakeDaemon(string path, byte[]? response)
        {
            Path = path;
            _listener.Bind(new UnixDomainSocketEndPoint(path));
            _listener.Listen(1);
            Request = Task.Run(() => ServeAsync(response));
        }

        public string Path { get; }

        public Task<byte[]> Request { get; }

        private async Task<byte[]> ServeAsync(byte[]? response)
        {
            using var client = await _listener.AcceptAsync(_stop.Token);
            var request = new byte[IpcHandshakeWireCodec.CreateHandshake().Length];
            await client.ReceiveAsync(request, SocketFlags.None, _stop.Token);
            if (response is null)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, _stop.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                return request;
            }

            await client.SendAsync(response, SocketFlags.None, _stop.Token);
            return request;
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Dispose();
            _stop.Dispose();
        }
    }
}
