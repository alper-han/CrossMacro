namespace CrossMacro.Daemon.Tests.Services;


public sealed class DaemonServiceTests
{
    [Fact]
    public async Task RunAsync_WhenTokenAlreadyCanceled_CompletesWithoutConnectionHandling()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        await using var socketPath = new TestSocketPath();
        var sessionHandler = new SessionHandler(security, virtualDevice, captureManager);
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPath.Path);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await service.RunAsync(cts.Token);

        Assert.Equal(0, security.ValidateCalls);

        if (permission.ConfiguredSocketPath is not null)
        {
            Assert.False(File.Exists(permission.ConfiguredSocketPath));
        }
    }

    [Fact]
    public async Task RunAsync_WhenFirstClientAuthorizationBlocks_ContinuesAcceptingAnotherClient()
    {
        var security = new BlockingValidationSecurityService();
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new RecordingSessionHandler();
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var firstClient = await ConnectAsync(socketPath);
        await security.WaitForFirstValidationAsync(TimeSpan.FromSeconds(2));

        using var secondClient = await ConnectAsync(socketPath);
        await security.WaitForSecondValidationAsync(TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, CancellationToken.None);

        Assert.Equal(2, security.ValidationCalls);
    }

    [Fact]
    public async Task RunAsync_WhenShuttingDown_RemovesSocketFileAndStopsAcceptingConnections()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        await using var socketPathScope = new TestSocketPath();
        var sessionHandler = new SessionHandler(security, virtualDevice, captureManager);
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        Assert.True(File.Exists(socketPath));

        await cts.CancelAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, CancellationToken.None);

        Assert.False(File.Exists(socketPath));

        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _ = await Assert.ThrowsAnyAsync<SocketException>(async () =>
            await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_WhenPermissionSetupFails_RemovesSocketFileBeforeReturningFailure()
    {
        var security = new FakeSecurityService();
        var permission = new ThrowingLinuxPermissionService(new UnauthorizedAccessException("chmod failed"));
        var sessionHandler = new RecordingSessionHandler();
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPathScope.Path);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RunAsync(CancellationToken.None));

        Assert.Equal("chmod failed", exception.Message);
        Assert.False(File.Exists(socketPathScope.Path));
        Assert.Equal(socketPathScope.Path, permission.ConfiguredSocketPath);
        Assert.Equal(0, security.ValidateCalls);
        Assert.Equal(0, sessionHandler.RunCalls);
    }

    [LinuxIntegrationFact]
    public async Task RunAsync_WhenClientValidationFails_DisposesRejectedClientDeterministically()
    {
        var security = new FakeSecurityService
        {
            ValidationResult = null,
            DisposeRejectedClient = false,
        };
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        await using var socketPathScope = new TestSocketPath();
        var sessionHandler = new SessionHandler(security, virtualDevice, captureManager);
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await security.WaitForValidationAsync(TimeSpan.FromSeconds(2));
        await AssertRemoteClosedAsync(client, TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, CancellationToken.None);

        Assert.True(security.ValidateCalls >= 1);
        Assert.Equal(0, security.DisconnectCalls);
    }

    [Fact]
    public async Task RunAsync_WhenClientValidationFails_ShouldNotCreateSessionHandler()
    {
        var security = new FakeSecurityService
        {
            ValidationResult = null,
            DisposeRejectedClient = true,
        };
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new RecordingSessionHandler();
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await security.WaitForValidationAsync(TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, CancellationToken.None);

        Assert.True(security.ValidateCalls >= 1);
        Assert.Equal(0, sessionHandler.RunCalls);
        Assert.Equal(0, security.DisconnectCalls);
    }

    [Fact]
    public async Task RunAsync_WhenClientValidationThrows_ShouldFailClosedWithoutCreatingSession()
    {
        var security = new FakeSecurityService
        {
            ValidationException = new TimeoutException("polkit timeout"),
        };
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new RecordingSessionHandler();
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await security.WaitForValidationAsync(TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, CancellationToken.None);

        Assert.True(security.ValidateCalls >= 1);
        Assert.Equal(0, sessionHandler.RunCalls);
        Assert.Equal(0, security.DisconnectCalls);
    }

    [LinuxIntegrationFact]
    public async Task RunAsync_WhenClientDisconnectsAfterValidation_LogsDisconnect()
    {
        const uint uid = 1001;
        const int pid = 9001;

        var security = new FakeSecurityService
        {
            ValidationResult = (uid, pid),
        };
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        await using var socketPathScope = new TestSocketPath();
        var sessionHandler = new SessionHandler(security, virtualDevice, captureManager);
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        var client = await ConnectAsync(socketPath);
        client.Dispose();

        await security.WaitForDisconnectAsync(TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, CancellationToken.None);

        Assert.Equal(1, security.DisconnectCalls);
        Assert.Equal(uid, security.LastDisconnectUid);
        Assert.Equal(pid, security.LastDisconnectPid);
    }

    [Fact]
    public async Task RunAsync_WhenValidatedClientConnects_UsesInjectedSessionFactoryAndLogsDisconnect()
    {
        const uint uid = 2001;
        const int pid = 4444;

        var security = new FakeSecurityService
        {
            ValidationResult = (uid, pid),
        };
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new RecordingSessionHandler();
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await sessionHandler.WaitForRunAsync(TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, CancellationToken.None);

        Assert.Equal(uid, sessionHandler.Uid);
        Assert.Equal(pid, sessionHandler.Pid);
        Assert.True(sessionHandler.Token.IsCancellationRequested);
        Assert.Equal(1, sessionHandler.RunCalls);
        Assert.Equal(1, security.DisconnectCalls);
    }

    [LinuxIntegrationFact]
    public async Task RunAsync_WhenValidatedSessionCompletes_ClientIsClosedAndDisconnectIsLogged()
    {
        const uint uid = 2003;
        const int pid = 6666;

        var security = new FakeSecurityService
        {
            ValidationResult = (uid, pid),
        };
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new CompletingSessionHandler();
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await sessionHandler.WaitForRunAsync(TimeSpan.FromSeconds(2));
        await AssertRemoteClosedAsync(client, TimeSpan.FromSeconds(2));
        await security.WaitForDisconnectAsync(TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, CancellationToken.None);

        Assert.Equal(1, sessionHandler.RunCalls);
        Assert.Equal(1, security.DisconnectCalls);
        Assert.Equal(uid, security.LastDisconnectUid);
        Assert.Equal(pid, security.LastDisconnectPid);
    }

    [LinuxIntegrationFact]
    public async Task RunAsync_WhenSessionFactoryThrows_StillLogsDisconnectAndClosesClient()
    {
        const uint uid = 2002;
        const int pid = 5555;

        var security = new FakeSecurityService
        {
            ValidationResult = (uid, pid),
        };
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new ThrowingSessionHandler(new InvalidOperationException("boom"));
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandler,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await security.WaitForValidationAsync(TimeSpan.FromSeconds(2));
        await security.WaitForDisconnectAsync(TimeSpan.FromSeconds(2));
        await AssertRemoteClosedAsync(client, TimeSpan.FromSeconds(2));

        await cts.CancelAsync();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, CancellationToken.None);

        Assert.Equal(1, sessionHandler.RunCalls);
        Assert.Equal(1, security.DisconnectCalls);
        Assert.Equal(uid, security.LastDisconnectUid);
        Assert.Equal(pid, security.LastDisconnectPid);
    }

    private static async Task<Socket> ConnectAsync(string socketPath)
    {
        var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        try
        {
            await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), CancellationToken.None);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task AssertRemoteClosedAsync(Socket client, TimeSpan timeout)
    {
        var buffer = new byte[1];
        using var cts = new CancellationTokenSource(timeout);

        try
        {
            var bytesRead = await client.ReceiveAsync(buffer.AsMemory(0, 1), SocketFlags.None, cts.Token);
            Assert.Equal(0, bytesRead);
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException("Timed out waiting for the daemon to close the client socket.", ex);
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.NotConnected)
        {
            _ = ex;
        }
        catch (ObjectDisposedException ex)
        {
            _ = ex;
        }
    }

    private sealed class BlockingValidationSecurityService : ISecurityService
    {
        private readonly TaskCompletionSource _firstValidationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondValidationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstValidation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _validationCalls;

        public int ValidationCalls => Volatile.Read(ref _validationCalls);

        public async Task<(uint Uid, int Pid)?> ValidateConnectionAsync(
            Socket client,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _validationCalls);
            if (call is 1)
            {
                _ = _firstValidationStarted.TrySetResult();
                await _releaseFirstValidation.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _ = _secondValidationStarted.TrySetResult();
            }

            return null;
        }

        public void LogDisconnect(uint uid, int pid, TimeSpan duration)
        {
        }

        public void LogCaptureStart(uint uid, int pid, bool mouse, bool kb)
        {
        }

        public void LogCaptureStop(uint uid, int pid)
        {
        }

        public void LogSimulation(uint uid, int pid, ushort type, ushort code, int value)
        {
        }

        public Task WaitForFirstValidationAsync(TimeSpan timeout) => _firstValidationStarted.Task.WaitAsync(timeout, TimeProvider.System, CancellationToken.None);

        public Task WaitForSecondValidationAsync(TimeSpan timeout) => _secondValidationStarted.Task.WaitAsync(timeout, TimeProvider.System, CancellationToken.None);
    }

    private sealed class FakeLinuxPermissionService : ILinuxPermissionService
    {
        private readonly TaskCompletionSource<string> _configuredPath = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? ConfiguredSocketPath { get; private set; }

        public void ConfigureSocketPermissions(string socketPath)
        {
            ConfiguredSocketPath = socketPath;
            _ = _configuredPath.TrySetResult(socketPath);
        }

        public Task<string> WaitForConfiguredPathAsync(TimeSpan timeout) => _configuredPath.Task.WaitAsync(timeout, TimeProvider.System, CancellationToken.None);
    }

    private sealed class ThrowingLinuxPermissionService(Exception exception) : ILinuxPermissionService
    {
        private readonly Exception _exception = exception;

        public string? ConfiguredSocketPath { get; private set; }

        public void ConfigureSocketPermissions(string socketPath)
        {
            ConfiguredSocketPath = socketPath;
            throw _exception;
        }
    }

    private sealed class TestSocketPath : IAsyncDisposable
    {
        public TestSocketPath()
        {
            Path = TestSocketPaths.CreateShort("cm-daemon");
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }

        public string Path { get; }

        public ValueTask DisposeAsync()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingSessionHandler : ISessionHandler
    {
        private readonly TaskCompletionSource _runStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RunCalls { get; private set; }
        public uint Uid { get; private set; }
        public int Pid { get; private set; }
        public CancellationToken Token { get; private set; }

        public async Task RunAsync(Socket client, uint uid, int pid, CancellationToken token)
        {
            RunCalls++;
            Uid = uid;
            Pid = pid;
            Token = token;
            _ = _runStarted.TrySetResult();

            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }

        public Task WaitForRunAsync(TimeSpan timeout) => _runStarted.Task.WaitAsync(timeout, TimeProvider.System, CancellationToken.None);
    }

    private sealed class CompletingSessionHandler : ISessionHandler
    {
        private readonly TaskCompletionSource _runStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RunCalls { get; private set; }

        public Task RunAsync(Socket client, uint uid, int pid, CancellationToken token)
        {
            RunCalls++;
            _ = _runStarted.TrySetResult();
            return Task.CompletedTask;
        }

        public Task WaitForRunAsync(TimeSpan timeout) => _runStarted.Task.WaitAsync(timeout, TimeProvider.System, CancellationToken.None);
    }

    private sealed class ThrowingSessionHandler(Exception exception) : ISessionHandler
    {
        private readonly Exception _exception = exception;

        public int RunCalls { get; private set; }

        public Task RunAsync(Socket client, uint uid, int pid, CancellationToken token)
        {
            RunCalls++;
            throw _exception;
        }
    }
}
