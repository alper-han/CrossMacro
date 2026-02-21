namespace CrossMacro.Daemon.Tests.Services;

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Daemon;
using CrossMacro.Daemon.Services;
using CrossMacro.Platform.Linux.Native.UInput;
using CrossMacro.TestInfrastructure;

public class DaemonServiceTests
{
    [LinuxIntegrationFact]
    public async Task RunAsync_WhenTokenAlreadyCanceled_CompletesWithoutConnectionHandling()
    {
        var security = new FakeSecurityService();
        var virtualDevice = new FakeVirtualDeviceManager();
        var captureManager = new FakeInputCaptureManager();
        var permission = new FakeLinuxPermissionService();
        var service = new DaemonService(security, virtualDevice, captureManager, permission);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await service.RunAsync(cts.Token);

        Assert.Equal(0, security.ValidateCalls);

        if (permission.ConfiguredSocketPath != null)
        {
            Assert.False(File.Exists(permission.ConfiguredSocketPath));
        }
    }

    [LinuxIntegrationFact]
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
        var service = new DaemonService(security, virtualDevice, captureManager, permission);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        using var client = await ConnectWithRetryAsync(socketPath, TimeSpan.FromSeconds(2));
        await security.WaitForValidationAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(security.ValidateCalls >= 1);
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
        var service = new DaemonService(security, virtualDevice, captureManager, permission);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var runTask = service.RunAsync(cts.Token);
        var socketPath = await permission.WaitForConfiguredPathAsync(TimeSpan.FromSeconds(2));

        var client = await ConnectWithRetryAsync(socketPath, TimeSpan.FromSeconds(2));
        client.Dispose();

        await security.WaitForDisconnectAsync(TimeSpan.FromSeconds(2));

        cts.Cancel();
        await runTask.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(1, security.DisconnectCalls);
        Assert.Equal(uid, security.LastDisconnectUid);
        Assert.Equal(pid, security.LastDisconnectPid);
    }

    private static async Task<Socket> ConnectWithRetryAsync(string socketPath, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
                return client;
            }
            catch (Exception ex)
            {
                lastException = ex;
                client.Dispose();
                await Task.Delay(25);
            }
        }

        throw new TimeoutException($"Unable to connect to daemon socket at '{socketPath}'. Last error: {lastException?.Message}");
    }

    private sealed class FakeSecurityService : ISecurityService
    {
        private readonly TaskCompletionSource _validated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public (uint Uid, int Pid)? ValidationResult { get; init; } = null;
        public bool DisposeRejectedClient { get; init; }
        public int ValidateCalls { get; private set; }
        public int DisconnectCalls { get; private set; }
        public uint LastDisconnectUid { get; private set; }
        public int LastDisconnectPid { get; private set; }

        public Task<(uint Uid, int Pid)?> ValidateConnectionAsync(Socket client)
        {
            ValidateCalls++;
            _validated.TrySetResult();

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
        public void StartCapture(bool captureMouse, bool captureKeyboard, Action<UInputNative.input_event> onEvent)
        {
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
}
