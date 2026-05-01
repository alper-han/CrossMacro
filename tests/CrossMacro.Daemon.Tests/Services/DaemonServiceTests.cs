namespace CrossMacro.Daemon.Tests.Services;

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Daemon;
using CrossMacro.Daemon.Services;
using CrossMacro.Infrastructure.Linux.Native.UInput;
using CrossMacro.TestInfrastructure;

public class DaemonServiceTests
{
    [LinuxFact]
    public async Task RunAsync_WhenTokenAlreadyCanceled_CompletesWithoutConnectionHandling()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        await using var socketPath = new TestSocketPath();
        var sessionHandler = new SessionHandler(security, virtualDevice, captureManager);
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPath.Path);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await service.RunAsync(cts.Token);

        Assert.Equal(0, security.ValidateCalls);

        if (permission.ConfiguredSocketPath != null)
        {
            Assert.False(File.Exists(permission.ConfiguredSocketPath));
        }
    }

    [LinuxFact]
    public async Task RunAsync_WhenShuttingDown_RemovesSocketFileAndStopsAcceptingConnections()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        await using var socketPathScope = new TestSocketPath();
        var sessionHandler = new SessionHandler(security, virtualDevice, captureManager);
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        Assert.True(File.Exists(socketPath));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.False(File.Exists(socketPath));

        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await Assert.ThrowsAnyAsync<SocketException>(async () =>
            await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath)));
    }

    [LinuxFact]
    public async Task RunAsync_WhenPermissionSetupFails_RemovesSocketFileBeforeReturningFailure()
    {
        var security = new FakeSecurityService();
        var permission = new ThrowingLinuxPermissionService(new UnauthorizedAccessException("chmod failed"));
        var sessionHandler = new RecordingSessionHandler();
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPathScope.Path);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RunAsync(CancellationToken.None));

        Assert.Equal("chmod failed", exception.Message);
        Assert.False(File.Exists(socketPathScope.Path));
        Assert.Equal(socketPathScope.Path, permission.ConfiguredSocketPath);
        Assert.Equal(0, security.ValidateCalls);
        Assert.Equal(0, sessionHandler.RunCalls);
    }

    [LinuxFact]
    public async Task RunAsync_WhenClientValidationFails_StillInvokesValidation()
    {
        var security = new FakeSecurityService
        {
            ValidationResult = null,
            DisposeRejectedClient = true
        };
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        await using var socketPathScope = new TestSocketPath();
        var sessionHandler = new SessionHandler(security, virtualDevice, captureManager);
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await security.WaitForValidationAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(security.ValidateCalls >= 1);
        Assert.Equal(0, security.DisconnectCalls);
    }

    [LinuxIntegrationFact]
    public async Task RunAsync_WhenClientValidationFails_DisposesRejectedClientDeterministically()
    {
        var security = new FakeSecurityService
        {
            ValidationResult = null,
            DisposeRejectedClient = false
        };
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        await using var socketPathScope = new TestSocketPath();
        var sessionHandler = new SessionHandler(security, virtualDevice, captureManager);
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await security.WaitForValidationAsync(TimeSpan.FromSeconds(2));
        await AssertRemoteClosedAsync(client, TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(security.ValidateCalls >= 1);
        Assert.Equal(0, security.DisconnectCalls);
    }

    [LinuxFact]
    public async Task RunAsync_WhenClientValidationFails_ShouldNotCreateSessionHandler()
    {
        var security = new FakeSecurityService
        {
            ValidationResult = null,
            DisposeRejectedClient = true
        };
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new RecordingSessionHandler();
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await security.WaitForValidationAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(security.ValidateCalls >= 1);
        Assert.Equal(0, sessionHandler.RunCalls);
        Assert.Equal(0, sessionHandlerFactory.CreateCalls);
        Assert.Equal(0, security.DisconnectCalls);
    }

    [LinuxFact]
    public async Task RunAsync_WhenClientValidationThrows_ShouldFailClosedWithoutCreatingSession()
    {
        var security = new FakeSecurityService
        {
            ValidationException = new TimeoutException("polkit timeout")
        };
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new RecordingSessionHandler();
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await security.WaitForValidationAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(security.ValidateCalls >= 1);
        Assert.Equal(0, sessionHandler.RunCalls);
        Assert.Equal(0, sessionHandlerFactory.CreateCalls);
        Assert.Equal(0, security.DisconnectCalls);
    }

    [LinuxIntegrationFact]
    public async Task RunAsync_WhenClientDisconnectsAfterValidation_LogsDisconnect()
    {
        const uint uid = 1001;
        const int pid = 9001;

        var security = new FakeSecurityService
        {
            ValidationResult = (uid, pid)
        };
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        await using var socketPathScope = new TestSocketPath();
        var sessionHandler = new SessionHandler(security, virtualDevice, captureManager);
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        var client = await ConnectAsync(socketPath);
        client.Dispose();

        await security.WaitForDisconnectAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(1, security.DisconnectCalls);
        Assert.Equal(uid, security.LastDisconnectUid);
        Assert.Equal(pid, security.LastDisconnectPid);
    }

    [LinuxFact]
    public async Task RunAsync_WhenValidatedClientConnects_UsesInjectedSessionFactoryAndLogsDisconnect()
    {
        const uint uid = 2001;
        const int pid = 4444;

        var security = new FakeSecurityService
        {
            ValidationResult = (uid, pid)
        };
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new RecordingSessionHandler();
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await sessionHandler.WaitForRunAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(uid, sessionHandler.Uid);
        Assert.Equal(pid, sessionHandler.Pid);
        Assert.True(sessionHandler.Token.IsCancellationRequested);
        Assert.Equal(1, sessionHandler.RunCalls);
        Assert.Equal(1, sessionHandlerFactory.CreateCalls);
        Assert.Equal(1, security.DisconnectCalls);
    }

    [LinuxIntegrationFact]
    public async Task RunAsync_WhenValidatedSessionCompletes_ClientIsClosedAndDisconnectIsLogged()
    {
        const uint uid = 2003;
        const int pid = 6666;

        var security = new FakeSecurityService
        {
            ValidationResult = (uid, pid)
        };
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new CompletingSessionHandler();
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await sessionHandler.WaitForRunAsync(TimeSpan.FromSeconds(2));
        await AssertRemoteClosedAsync(client, TimeSpan.FromSeconds(2));
        await security.WaitForDisconnectAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

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
            ValidationResult = (uid, pid)
        };
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        var sessionHandler = new ThrowingSessionHandler(new InvalidOperationException("boom"));
        var sessionHandlerFactory = new RecordingSessionHandlerFactory(sessionHandler);
        await using var socketPathScope = new TestSocketPath();
        var service = new DaemonService(
            security,
            permission,
            sessionHandlerFactory,
            socketPathScope.Path);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectAsync(socketPath);
        await security.WaitForValidationAsync(TimeSpan.FromSeconds(2));
        await security.WaitForDisconnectAsync(TimeSpan.FromSeconds(2));
        await AssertRemoteClosedAsync(client, TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

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
            await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
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
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private sealed class FakeSecurityService : ISecurityService
    {
        private readonly TaskCompletionSource _validated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public (uint Uid, int Pid)? ValidationResult { get; init; } = null;
        public bool DisposeRejectedClient { get; init; }
        public Exception? ValidationException { get; init; }
        public int ValidateCalls { get; private set; }
        public int DisconnectCalls { get; private set; }
        public uint LastDisconnectUid { get; private set; }
        public int LastDisconnectPid { get; private set; }

        public Task<(uint Uid, int Pid)?> ValidateConnectionAsync(Socket client)
        {
            ValidateCalls++;
            _validated.TrySetResult();

            if (ValidationException != null)
            {
                throw ValidationException;
            }

            if (ValidationResult == null && DisposeRejectedClient)
            {
                client.Dispose();
            }

            return Task.FromResult(ValidationResult);
        }

        public void LogDisconnect(uint uid, int pid, TimeSpan duration)
        {
            DisconnectCalls++;
            LastDisconnectUid = uid;
            LastDisconnectPid = pid;
            _disconnected.TrySetResult();
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

        public Task WaitForValidationAsync(TimeSpan timeout) => _validated.Task.WaitAsync(timeout);

        public Task WaitForDisconnectAsync(TimeSpan timeout) => _disconnected.Task.WaitAsync(timeout);
    }

    private sealed class FakeVirtualDeviceManager : IVirtualDeviceManager
    {
        public void Configure(int width, int height)
        {
        }

        public void SendEvent(ushort type, ushort code, int value)
        {
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeInputCaptureManager : IInputCaptureManager
    {
        public CaptureStartResult StartCapture(bool captureMouse, bool captureKeyboard, Action<UInputNative.input_event> onEvent)
        {
            return CaptureStartResult.Started(startedDeviceCount: 1);
        }

        public void StopCapture()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeLinuxPermissionService : ILinuxPermissionService
    {
        private readonly TaskCompletionSource<string> _configuredPath = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? ConfiguredSocketPath { get; private set; }

        public void ConfigureSocketPermissions(string socketPath)
        {
            ConfiguredSocketPath = socketPath;
            _configuredPath.TrySetResult(socketPath);
        }

        public Task<string> WaitForConfiguredPathAsync(TimeSpan timeout) => _configuredPath.Task.WaitAsync(timeout);
    }

    private sealed class ThrowingLinuxPermissionService : ILinuxPermissionService
    {
        private readonly Exception _exception;

        public ThrowingLinuxPermissionService(Exception exception)
        {
            _exception = exception;
        }

        public string? ConfiguredSocketPath { get; private set; }

        public void ConfigureSocketPermissions(string socketPath)
        {
            ConfiguredSocketPath = socketPath;
            throw _exception;
        }
    }

    private sealed class RecordingSessionHandlerFactory : ISessionHandlerFactory
    {
        private readonly ISessionHandler _handler;

        public RecordingSessionHandlerFactory(ISessionHandler handler)
        {
            _handler = handler;
        }

        public int CreateCalls { get; private set; }

        public ISessionHandler Create()
        {
            CreateCalls++;
            return _handler;
        }
    }

    private sealed class TestSocketPath : IAsyncDisposable
    {
        public TestSocketPath()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crossmacro-daemon-test-{Guid.NewGuid():N}.sock");
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
            _runStarted.TrySetResult();

            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }

        public Task WaitForRunAsync(TimeSpan timeout) => _runStarted.Task.WaitAsync(timeout);
    }

    private sealed class CompletingSessionHandler : ISessionHandler
    {
        private readonly TaskCompletionSource _runStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RunCalls { get; private set; }

        public Task RunAsync(Socket client, uint uid, int pid, CancellationToken token)
        {
            RunCalls++;
            _runStarted.TrySetResult();
            return Task.CompletedTask;
        }

        public Task WaitForRunAsync(TimeSpan timeout) => _runStarted.Task.WaitAsync(timeout);
    }

    private sealed class ThrowingSessionHandler : ISessionHandler
    {
        private readonly Exception _exception;

        public ThrowingSessionHandler(Exception exception)
        {
            _exception = exception;
        }

        public int RunCalls { get; private set; }

        public Task RunAsync(Socket client, uint uid, int pid, CancellationToken token)
        {
            RunCalls++;
            throw _exception;
        }
    }
}
